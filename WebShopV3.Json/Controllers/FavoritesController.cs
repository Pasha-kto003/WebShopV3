using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;
using FavoriteResult = WebShopV3.Json.Models.FavoriteResult;
using Favorite = WebShopV3.Json.Models.Favorite;

namespace WebShopV3.Json.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly IFavoriteService _favoriteService;
        private readonly JsonDataService _jsonData;
        private readonly ILogger<FavoritesController> _logger;
        private const string GUEST_ID_COOKIE = "FavoriteGuestId";
        private const int GUEST_ID_EXPIRE_DAYS = 30;

        public FavoritesController(
            IFavoriteService favoriteService,
            JsonDataService jsonData,
            ILogger<FavoritesController> logger)
        {
            _favoriteService = favoriteService;
            _jsonData = jsonData;
            _logger = logger;
        }

        // Вспомогательный метод для получения GuestId
        private string GetOrCreateGuestId()
        {
            var guestId = Request.Cookies[GUEST_ID_COOKIE];

            if (string.IsNullOrEmpty(guestId))
            {
                guestId = Guid.NewGuid().ToString();

                Response.Cookies.Append(GUEST_ID_COOKIE, guestId, new CookieOptions
                {
                    Expires = DateTime.UtcNow.AddDays(GUEST_ID_EXPIRE_DAYS),
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Strict,
                    Secure = Request.IsHttps
                });
            }

            return guestId;
        }

        // Вспомогательный метод для получения UserId
        private int? GetUserId()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdClaim, out int userId))
                {
                    return userId;
                }
            }
            return null;
        }

        // GET: /Favorites - Страница избранного
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            var guestId = GetOrCreateGuestId();

            // Получаем избранное с продуктами
            var favorites = await GetFavoritesWithProductsAsync(userId, guestId);

            // Получаем счетчик для отображения
            var totalCount = await GetFavoriteCountAsync(userId, guestId);

            ViewBag.TotalCount = totalCount;
            ViewBag.IsGuest = !userId.HasValue;

            return View(favorites);
        }

        // POST: /Favorites/Add - Добавить в избранное (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(string productType, int productId)
        {
            try
            {
                var userId = GetUserId();
                var guestId = GetOrCreateGuestId();

                var result = await AddToFavoritesAsync(userId, guestId, productType, productId);

                if (result.Success)
                {
                    return Json(new
                    {
                        success = true,
                        message = result.Message,
                        count = result.TotalCount,
                        favoriteId = result.Favorite?.Id
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = result.Message,
                        count = result.TotalCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении в избранное");
                return Json(new
                {
                    success = false,
                    message = "Ошибка при добавлении в избранное"
                });
            }
        }

        // POST: /Favorites/Remove - Удалить из избранного (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int favoriteId)
        {
            try
            {
                var userId = GetUserId();
                var guestId = GetOrCreateGuestId();

                var result = await RemoveFromFavoritesAsync(userId, guestId, favoriteId);

                if (result.Success)
                {
                    return Json(new
                    {
                        success = true,
                        message = result.Message,
                        count = result.TotalCount
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = result.Message,
                        count = result.TotalCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении из избранного");
                return Json(new
                {
                    success = false,
                    message = "Ошибка при удалении из избранного"
                });
            }
        }

        // POST: /Favorites/RemoveByProduct - Удалить по продукту (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveByProduct(string productType, int productId)
        {
            try
            {
                var userId = GetUserId();
                var guestId = GetOrCreateGuestId();

                var result = await RemoveByProductAsync(userId, guestId, productType, productId);

                if (result.Success)
                {
                    return Json(new
                    {
                        success = true,
                        message = result.Message,
                        count = result.TotalCount
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = result.Message,
                        count = result.TotalCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении из избранного");
                return Json(new
                {
                    success = false,
                    message = "Ошибка при удаления из избранного"
                });
            }
        }

        // GET: /Favorites/Count - Получить количество (AJAX)
        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var userId = GetUserId();
            var guestId = GetOrCreateGuestId();

            var count = await GetFavoriteCountAsync(userId, guestId);

            return Json(new { count });
        }

        // GET: /Favorites/Check - Проверить, есть ли в избранном (AJAX)
        [HttpGet]
        public async Task<IActionResult> Check(string productType, int productId)
        {
            var userId = GetUserId();
            var guestId = GetOrCreateGuestId();

            var isFavorite = await IsProductInFavoritesAsync(userId, guestId, productType, productId);

            return Json(new { isFavorite });
        }

        public async Task<IActionResult> Recent()
        {
            var userId = GetUserId();
            var guestId = GetOrCreateGuestId();

            // Получаем последние 3 избранных товара
            var favorites = await GetFavoritesWithProductsAsync(userId, guestId);
            var recentFavorites = favorites.Take(3).ToList();

            return PartialView("_FavoriteDropdownItems", recentFavorites);
        }

        // И метод для очистки всех:
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAll()
        {
            var userId = GetUserId();
            var guestId = GetOrCreateGuestId();

            try
            {
                var favorites = await GetFavoritesAsync(userId, guestId);

                if (!favorites.Any())
                {
                    return Json(new { success = false, message = "Избранное уже пусто" });
                }

                // Удаляем все избранные
                var allFavorites = await _jsonData.GetAllAsync<Models.Favorite>("Favorites");
                var favoritesToRemove = allFavorites
                    .Where(f => (userId.HasValue && f.UserId == userId) ||
                               (!string.IsNullOrEmpty(guestId) && f.GuestId == guestId))
                    .ToList();

                foreach (var favorite in favoritesToRemove)
                {
                    allFavorites.Remove(favorite);
                }

                await _jsonData.SaveAllAsync("Favorites", allFavorites);

                return Json(new
                {
                    success = true,
                    message = $"Очищено {favorites.Count} товаров из избранного"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке избранного");
                return Json(new { success = false, message = "Ошибка при очистке избранного" });
            }
        }

        // POST: /Favorites/Migrate - Мигрировать гостевые избранные
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Migrate()
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var guestId = Request.Cookies[GUEST_ID_COOKIE];

            if (string.IsNullOrEmpty(guestId))
            {
                TempData["Message"] = "Нет гостевых избранных для миграции";
                return RedirectToAction("Index");
            }

            var result = await MigrateGuestFavoritesAsync(guestId, userId.Value);

            if (result.Success)
            {
                // Удаляем куку гостя
                Response.Cookies.Delete(GUEST_ID_COOKIE);

                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Index");
        }

        // GET: /Favorites/Partial - Получить частичное представление для корзины/шапки
        public async Task<IActionResult> Partial()
        {
            var userId = GetUserId();
            var guestId = GetOrCreateGuestId();

            var count = await GetFavoriteCountAsync(userId, guestId);

            ViewBag.FavoriteCount = count;
            ViewBag.IsGuest = !userId.HasValue;

            return PartialView("_FavoritePartial");
        }

        // Вспомогательные методы (заменяем IFavoriteService)

        private async Task<List<Models.Favorite>> GetFavoritesAsync(int? userId, string guestId)
        {
            var allFavorites = await _jsonData.GetAllAsync<Favorite>("Favorites");

            return allFavorites.Where(f =>
                (userId.HasValue && f.UserId == userId) ||
                (!string.IsNullOrEmpty(guestId) && f.GuestId == guestId)
            ).ToList();
        }

        private async Task<List<Models.FavoriteWithProduct>> GetFavoritesWithProductsAsync(int? userId, string guestId)
        {
            var favorites = await GetFavoritesAsync(userId, guestId);
            var result = new List<Models.FavoriteWithProduct>();

            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");

            foreach (var favorite in favorites)
            {
                if (favorite.ProductType == "Computer")
                {
                    var computer = computers.FirstOrDefault(c => c.Id == favorite.ProductId);
                    result.Add(new Models.FavoriteWithProduct
                    {
                        Favorite = favorite,
                        Computer = computer,
                        Component = null
                    });
                }
                else if (favorite.ProductType == "Component")
                {
                    var component = components.FirstOrDefault(c => c.Id == favorite.ProductId);
                    result.Add(new Models.FavoriteWithProduct
                    {
                        Favorite = favorite,
                        Computer = null,
                        Component = component
                    });
                }
            }

            return result.OrderByDescending(f => f.Favorite.AddedAt).ToList();
        }

        private async Task<int> GetFavoriteCountAsync(int? userId, string guestId)
        {
            var favorites = await GetFavoritesAsync(userId, guestId);
            return favorites.Count;
        }

        private async Task<FavoriteResult> AddToFavoritesAsync(int? userId, string guestId, string productType, int productId)
        {
            // Проверяем, не добавлен ли уже товар
            var existingFavorites = await GetFavoritesAsync(userId, guestId);
            var existing = existingFavorites.FirstOrDefault(f =>
                f.ProductType == productType && f.ProductId == productId);

            if (existing != null)
            {
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Товар уже в избранном",
                    TotalCount = existingFavorites.Count
                };
            }

            // Проверяем существование товара
            if (productType == "Computer")
            {
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                if (!computers.Any(c => c.Id == productId))
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = "Компьютер не найден",
                        TotalCount = existingFavorites.Count
                    };
                }
            }
            else if (productType == "Component")
            {
                var components = await _jsonData.GetAllAsync<Component>("Components");
                if (!components.Any(c => c.Id == productId))
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = "Комплектующее не найдено",
                        TotalCount = existingFavorites.Count
                    };
                }
            }

            // Создаем новое избранное
            var favorite = new Favorite
            {
                UserId = userId,
                GuestId = userId.HasValue ? null : guestId,
                ProductType = productType,
                ProductId = productId,
                AddedAt = DateTime.UtcNow,
                LastViewed = null
            };

            await _jsonData.CreateAsync("Favorites", favorite);

            return new FavoriteResult
            {
                Success = true,
                Message = "Товар добавлен в избранное",
                TotalCount = existingFavorites.Count + 1,
                Favorite = favorite
            };
        }

        private async Task<FavoriteResult> RemoveFromFavoritesAsync(int? userId, string guestId, int favoriteId)
        {
            var favorites = await GetFavoritesAsync(userId, guestId);
            var favorite = favorites.FirstOrDefault(f => f.Id == favoriteId);

            if (favorite == null)
            {
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Избранное не найдено",
                    TotalCount = favorites.Count
                };
            }

            var success = await _jsonData.DeleteAsync<Models.Favorite>("Favorites", favoriteId);
            if (!success)
            {
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Ошибка при удалении",
                    TotalCount = favorites.Count
                };
            }

            return new FavoriteResult
            {
                Success = true,
                Message = "Товар удален из избранного",
                TotalCount = favorites.Count - 1
            };
        }

        private async Task<FavoriteResult> RemoveByProductAsync(int? userId, string guestId, string productType, int productId)
        {
            var favorites = await GetFavoritesAsync(userId, guestId);
            var favorite = favorites.FirstOrDefault(f =>
                f.ProductType == productType && f.ProductId == productId);

            if (favorite == null)
            {
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Товар не найден в избранном",
                    TotalCount = favorites.Count
                };
            }

            var success = await _jsonData.DeleteAsync<Models.Favorite>("Favorites", favorite.Id);
            if (!success)
            {
                return new Models.FavoriteResult
                {
                    Success = false,
                    Message = "Ошибка при удалении",
                    TotalCount = favorites.Count
                };
            }

            return new Models.FavoriteResult
            {
                Success = true,
                Message = "Товар удален из избранного",
                TotalCount = favorites.Count - 1
            };
        }

        private async Task<bool> IsProductInFavoritesAsync(int? userId, string guestId, string productType, int productId)
        {
            var favorites = await GetFavoritesAsync(userId, guestId);
            return favorites.Any(f => f.ProductType == productType && f.ProductId == productId);
        }

        private async Task<FavoriteResult> MigrateGuestFavoritesAsync(string guestId, int userId)
        {
            var guestFavorites = await GetFavoritesAsync(null, guestId);

            if (!guestFavorites.Any())
            {
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Нет гостевых избранных для миграции"
                };
            }

            // Получаем все избранные
            var allFavorites = await _jsonData.GetAllAsync<Favorite>("Favorites");

            // Находим избранные пользователя
            var userFavorites = allFavorites.Where(f => f.UserId == userId).ToList();

            int migratedCount = 0;

            foreach (var guestFavorite in guestFavorites)
            {
                // Проверяем, нет ли уже такого товара у пользователя
                var exists = userFavorites.Any(f =>
                    f.ProductType == guestFavorite.ProductType &&
                    f.ProductId == guestFavorite.ProductId);

                if (!exists)
                {
                    // Создаем новую запись для пользователя
                    var newFavorite = new Favorite
                    {
                        UserId = userId,
                        GuestId = null,
                        ProductType = guestFavorite.ProductType,
                        ProductId = guestFavorite.ProductId,
                        AddedAt = guestFavorite.AddedAt,
                        LastViewed = guestFavorite.LastViewed
                    };

                    allFavorites.Add(newFavorite);
                    migratedCount++;
                }

                // Удаляем гостевую запись
                allFavorites.Remove(guestFavorite);
            }

            // Сохраняем изменения
            await _jsonData.SaveAllAsync("Favorites", allFavorites);

            return new FavoriteResult
            {
                Success = true,
                Message = $"Мигрировано {migratedCount} избранных товаров",
                TotalCount = userFavorites.Count + migratedCount
            };
        }
    }
}