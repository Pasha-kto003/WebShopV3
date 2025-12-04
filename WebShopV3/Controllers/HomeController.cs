using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using WebShopV3.Models;

namespace WebShopV3.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var computers = await _context.Computers
                .Where(c => c.Quantity > 0)
                .Take(8)
                .ToListAsync();

            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;

            return View(computers);
        }

        [AllowAnonymous]
        public async Task<IActionResult> ComputerDetails(int? id, int page = 1)
        {
            if (id == null) return NotFound();

            var computer = await _context.Computers
                .Include(c => c.ComputerComponents)
                    .ThenInclude(cc => cc.Component)
                        .ThenInclude(comp => comp.ComponentCharacteristics)
                            .ThenInclude(cc => cc.Characteristic)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (computer == null) return NotFound();

            AddToRecentlyViewed(computer.Id, computer.Name, computer.Price, computer.ImageUrl);

            var recentlyViewed = GetRecentlyViewedComputers();

            ViewBag.RecentlyViewed = recentlyViewed
                .Where(c => c.Id != id.Value)
                .Take(5)
                .ToList();

            var componentTypes = computer.ComputerComponents
                .Select(cc => cc.Component.Type)
                .Distinct()
                .ToList();

            var recommendedComputers = await _context.Computers
                .Where(c => c.Quantity > 0 && c.Id != id)
                .Take(6)
                .ToListAsync();

            var recommendedComponents = await _context.Components
                .Where(c => c.Quantity > 0 && componentTypes.Contains(c.Type))
                .Take(6)
                .ToListAsync();

            // Передаем через ViewBag
            ViewBag.RecommendedComputers = recommendedComputers;
            ViewBag.RecommendedComponents = recommendedComponents;

            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;

            ViewBag.CurrentComputerId = id.Value;
            ViewBag.RecentlyViewedCount = recentlyViewed.Count;

            return View(computer);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Catalog(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice, string productType = "all")
        {
            ViewBag.SearchQuery = search;
            ViewBag.SortBy = sortBy;
            ViewBag.ComponentType = componentType;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.ProductType = productType;

            ViewBag.ComponentTypes = await _context.Components
                .Select(c => c.Type)
                .Distinct()
                .ToListAsync();

            var computers = await GetFilteredComputers(search, sortBy, componentType, minPrice, maxPrice);
            var components = await GetFilteredComponents(search, sortBy, componentType, minPrice, maxPrice);

            var viewModel = new CatalogViewModel
            {
                Computers = computers,
                Components = components,
                ProductType = productType
            };


            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;

            return View(viewModel);
        }


        [HttpGet]
        public async Task<IActionResult> SearchProducts(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice, string productType = "all")
        {
            var computers = await GetFilteredComputers(search, sortBy, componentType, minPrice, maxPrice);
            var components = await GetFilteredComponents(search, sortBy, componentType, minPrice, maxPrice);

            // ФИЛЬТРАЦИЯ ПО ТИПУ ТОВАРА
            if (productType == "computers")
            {
                components = new List<Component>(); // Очищаем комплектующие
            }
            else if (productType == "components")
            {
                computers = new List<Computer>(); // Очищаем компьютеры
            }
            
            // Если "all" - оставляем оба списка

            var viewModel = new CatalogViewModel
            {
                Computers = computers,
                Components = components,
                ProductType = productType
            };

            return PartialView("_ComputerListPartial", viewModel);
        }

        private async Task<List<Computer>> GetFilteredComputers(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice)
        {
            var query = _context.Computers
                .Include(c => c.ComputerComponents)
                .ThenInclude(cc => cc.Component)
                .Where(c => c.Quantity > 0)
                .AsQueryable();

            // Поиск по названию и описанию
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    c.Description.ToLower().Contains(search) ||
                    c.ComputerComponents.Any(cc => cc.Component.Name.ToLower().Contains(search))
                );
            }

            // Фильтр по типу комплектующих
            if (!string.IsNullOrEmpty(componentType) && componentType != "all")
            {
                query = query.Where(c =>
                    c.ComputerComponents.Any(cc => cc.Component.Type == componentType)
                );
            }

            // Фильтр по цене
            if (minPrice.HasValue)
            {
                query = query.Where(c => c.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(c => c.Price <= maxPrice.Value);
            }

            // Сортировка
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(c => c.Price),
                "price_desc" => query.OrderByDescending(c => c.Price),
                "name_asc" => query.OrderBy(c => c.Name),
                "name_desc" => query.OrderByDescending(c => c.Name),
                "newest" => query.OrderByDescending(c => c.Id),
                _ => query.OrderBy(c => c.Id) // по умолчанию
            };

            return await query.ToListAsync();
        }

        private async Task<List<Component>> GetFilteredComponents(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice)
        {
            var query = _context.Components
                .Include(c => c.ComponentCharacteristics)
                    .ThenInclude(cc => cc.Characteristic)
                .Where(c => c.Quantity > 0)
                .AsQueryable();

            // Поиск по названию и описанию
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    c.Description.ToLower().Contains(search) ||
                    c.Specifications.ToLower().Contains(search)
                );
            }

            // Фильтр по типу комплектующих
            if (!string.IsNullOrEmpty(componentType) && componentType != "all")
            {
                query = query.Where(c => c.Type == componentType);
            }

            // Фильтр по цене
            if (minPrice.HasValue)
            {
                query = query.Where(c => c.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(c => c.Price <= maxPrice.Value);
            }

            // Сортировка
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(c => c.Price),
                "price_desc" => query.OrderByDescending(c => c.Price),
                "name_asc" => query.OrderBy(c => c.Name),
                "name_desc" => query.OrderByDescending(c => c.Name),
                "newest" => query.OrderByDescending(c => c.Id),
                _ => query.OrderBy(c => c.Id)
            };

            return await query.ToListAsync();
        }


        // Вспомогательный класс для хранения в куках (добавить как вложенный класс)
        private class RecentlyViewedItem
        {
            public int ComputerId { get; set; }
            public string ComputerName { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string? ImageUrl { get; set; }
            public DateTime ViewedAt { get; set; }
        }

        private const string RecentlyViewedCookieName = "WebShopV3_RecentlyViewed";

        /// <summary>
        /// Добавляет компьютер в историю просмотров (куки)
        /// </summary>
        private void AddToRecentlyViewed(int computerId, string computerName, decimal price, string? imageUrl)
        {
            try
            {
                var history = GetRecentlyViewedFromCookie();

                // Проверяем, не просматривалось ли недавно (в течение 2 часов)
                var existing = history.FirstOrDefault(x => x.ComputerId == computerId);
                if (existing != null && (DateTime.UtcNow - existing.ViewedAt).TotalHours < 2)
                {
                    // Обновляем время просмотра
                    existing.ViewedAt = DateTime.UtcNow;
                }
                else
                {
                    // Удаляем дубликаты если есть
                    history.RemoveAll(x => x.ComputerId == computerId);

                    // Добавляем новый просмотр в начало
                    history.Insert(0, new RecentlyViewedItem
                    {
                        ComputerId = computerId,
                        ComputerName = TruncateString(computerName, 100),
                        Price = price,
                        ImageUrl = TruncateString(imageUrl, 250),
                        ViewedAt = DateTime.UtcNow
                    });

                    // Ограничиваем количество элементов (макс. 20)
                    if (history.Count > 20)
                    {
                        history = history.Take(20).ToList();
                    }
                }

                SaveHistoryToCookie(history);
            }
            catch
            {
                // Игнорируем ошибки с куками
            }
        }

        /// <summary>
        /// Получает список недавно просмотренных компьютеров из БД
        /// </summary>
        private List<Computer> GetRecentlyViewedComputers()
        {
            var recentlyViewedItems = GetRecentlyViewedFromCookie();
            if (!recentlyViewedItems.Any())
                return new List<Computer>();

            var computerIds = recentlyViewedItems.Select(x => x.ComputerId).ToList();

            // Используем синхронный вызов через Task.Run для избежания deadlock в ASP.NET
            var computers = _context.Computers
                .Include(c => c.ComputerComponents)
                    .ThenInclude(cc => cc.Component)
                .Where(c => computerIds.Contains(c.Id))
                .AsEnumerable()
                .OrderBy(c => computerIds.IndexOf(c.Id)) // Сохраняем порядок из куки
                .Take(6) // Берем на 1 больше, чтобы потом исключить текущий
                .ToList();

            return computers;
        }

        /// <summary>
        /// Получает историю просмотров из куки
        /// </summary>
        private List<RecentlyViewedItem> GetRecentlyViewedFromCookie()
        {
            var cookie = Request.Cookies[RecentlyViewedCookieName];
            if (string.IsNullOrEmpty(cookie))
                return new List<RecentlyViewedItem>();

            try
            {
                return JsonSerializer.Deserialize<List<RecentlyViewedItem>>(cookie)
                    ?? new List<RecentlyViewedItem>();
            }
            catch
            {
                return new List<RecentlyViewedItem>();
            }
        }

        /// <summary>
        /// Сохраняет историю просмотров в куки
        /// </summary>
        private void SaveHistoryToCookie(List<RecentlyViewedItem> history)
        {
            var serialized = JsonSerializer.Serialize(history);

            var options = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(45), // 1.5 месяца
                HttpOnly = false, // Чтобы можно было очистить через JS
                IsEssential = true,
                SameSite = SameSiteMode.Strict,
                Secure = Request.IsHttps,
                Path = "/"
            };

            Response.Cookies.Append(RecentlyViewedCookieName, serialized, options);
        }

        /// <summary>
        /// Очищает историю просмотров
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearViewHistory()
        {
            try
            {
                Response.Cookies.Delete(RecentlyViewedCookieName);
                TempData["SuccessMessage"] = "История просмотров успешно очищена";
            }
            catch
            {
                TempData["ErrorMessage"] = "Не удалось очистить историю просмотров";
            }

            // Возвращаем на предыдущую страницу
            var returnUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Обрезает строку до указанной длины
        /// </summary>
        private string? TruncateString(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// API метод для получения истории просмотров (для AJAX)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetRecentlyViewedJson()
        {
            var recentlyViewed = GetRecentlyViewedComputers();
            var currentComputerId = HttpContext.Request.Query["exclude"].FirstOrDefault();

            var result = recentlyViewed
                .Where(c => currentComputerId == null || c.Id.ToString() != currentComputerId)
                .Take(4)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    price = c.Price.ToString("C"),
                    imageUrl = c.ImageUrl ?? "/images/default-computer.png",
                    componentCount = c.ComputerComponents?.Count ?? 0,
                    cpu = c.ComputerComponents?
                        .Select(cc => cc.Component)
                        .FirstOrDefault(comp => comp.Type == "CPU")?.Name ?? "Не указан",
                    gpu = c.ComputerComponents?
                        .Select(cc => cc.Component)
                        .FirstOrDefault(comp => comp.Type == "GPU")?.Name ?? "Не указан",
                    url = Url.Action("ComputerDetails", "Computer", new { id = c.Id })
                })
                .ToList();

            return Json(new { success = true, data = result });
        }

        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Contact()
        {
            return View();
        }
        // Вспомогательный класс для хранения в куках (добавить как вложенный класс)
    }


}
