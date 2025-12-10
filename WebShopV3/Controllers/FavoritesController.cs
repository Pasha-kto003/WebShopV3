// Controllers/FavoritesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebShopV3.Models;
using WebShopV3.Services;

namespace WebShopV3.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FavoritesController> _logger;
        private const string GUEST_ID_COOKIE = "FavoriteGuestId";
        private const int GUEST_ID_EXPIRE_DAYS = 30;

        public FavoritesController(
            IFavoriteService favoriteService,
            ApplicationDbContext context,
            ILogger<FavoritesController> logger)
        {
            _favoriteService = favoriteService;
            _context = context;
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
            var favorites = await _favoriteService.GetFavoritesWithProductsAsync(userId, guestId);

            // Получаем счетчик для отображения
            var totalCount = await _favoriteService.GetFavoriteCountAsync(userId, guestId);

            ViewBag.TotalCount = totalCount;
            ViewBag.IsGuest = !userId.HasValue;

            // Используем List<FavoriteWithProduct> вместо var
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

                var result = await _favoriteService.AddToFavoritesAsync(userId, guestId, productType, productId);

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

                var result = await _favoriteService.RemoveFromFavoritesAsync(userId, guestId, favoriteId);

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

                var result = await _favoriteService.RemoveByProductAsync(userId, guestId, productType, productId);

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

            var count = await _favoriteService.GetFavoriteCountAsync(userId, guestId);

            return Json(new { count });
        }

        // GET: /Favorites/Check - Проверить, есть ли в избранном (AJAX)
        [HttpGet]
        public async Task<IActionResult> Check(string productType, int productId)
        {
            var userId = GetUserId();
            var guestId = GetOrCreateGuestId();

            var isFavorite = await _favoriteService.IsProductInFavoritesAsync(userId, guestId, productType, productId);

            return Json(new { isFavorite });
        }

        public async Task<IActionResult> Recent()
        {
            var userId = GetUserId();
            var guestId = GetOrCreateGuestId();

            // Получаем последние 3 избранных товара
            var favorites = await _favoriteService.GetFavoritesWithProductsAsync(userId, guestId);
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
                var favorites = await _favoriteService.GetFavoritesAsync(userId, guestId);

                if (!favorites.Any())
                {
                    return Json(new { success = false, message = "Избранное уже пусто" });
                }

                _context.Favorites.RemoveRange(favorites);
                await _context.SaveChangesAsync();

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
                return RedirectToAction("Login", "Account");
            }

            var guestId = Request.Cookies[GUEST_ID_COOKIE];

            if (string.IsNullOrEmpty(guestId))
            {
                TempData["Message"] = "Нет гостевых избранных для миграции";
                return RedirectToAction("Index");
            }

            var result = await _favoriteService.MigrateGuestFavoritesAsync(guestId, userId.Value);

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

            var count = await _favoriteService.GetFavoriteCountAsync(userId, guestId);

            ViewBag.FavoriteCount = count;
            ViewBag.IsGuest = !userId.HasValue;

            return PartialView("_FavoritePartial");
        }
    }
}