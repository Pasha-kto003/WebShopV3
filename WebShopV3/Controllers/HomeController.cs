using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using WebShopV3.Models;
using WebShopV3.Models.DTO;

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

            var bestsellers = await _context.Components
                .Where(c => c.Quantity > 0)
                .OrderByDescending(c => c.Id)
                .Take(6)
                .ToListAsync();

            // Получаем статистику для View
            ViewBag.TotalComponents = await _context.Components
                .Where(c => c.Quantity > 0)
                .CountAsync();

            ViewBag.TotalTypes = await _context.Components
                .Select(c => c.Type)
                .Distinct()
                .CountAsync();

            // Получаем компоненты по категориям
            var categoryData = await GetComponentCategoriesDataAsync();
            ViewBag.ComponentCategoriesData = categoryData;

            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;
            ViewBag.Bestsellers = bestsellers;
            ViewBag.ComponentTypes = await _context.Components
                .Select(c => c.Type)
                .Distinct()
                .Where(t => !string.IsNullOrEmpty(t))
                .Take(8)
                .ToListAsync();

            return View(computers);
        }

        [AllowAnonymous]
        public async Task<IActionResult> ComponentDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var component = await _context.Components
                .Include(c => c.ComponentCharacteristics)
                    .ThenInclude(cc => cc.Characteristic)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (component == null)
            {
                return NotFound();
            }

            // Получаем связанные компоненты (того же типа)
            var relatedComponents = await _context.Components
                .Where(c => c.Type == component.Type && c.Id != component.Id && c.Quantity > 0)
                .OrderByDescending(c => c.Id)
                .Take(4)
                .ToListAsync();

            // Передаем данные через ViewBag и ViewData
            ViewBag.RelatedComponents = relatedComponents;
            ViewBag.ComponentCharacteristics = component.ComponentCharacteristics.ToList();

            var recentlyViewed = GetRecentlyViewedComputers();

            ViewBag.RecentlyViewed = recentlyViewed
                .Where(c => c.Id != id.Value)
                .Take(5)
                .ToList();

            var componentTypes = _context.Components
                .Select(cc => cc.Type)
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

            // Получаем корзину для отображения количества
            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);
            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;

            return View(component); // Передаем сам компонент как модель
        }

        // Новый метод для получения данных категорий
        private async Task<List<CategoryData>> GetComponentCategoriesDataAsync()
        {
            var categoryTypes = new[] { "CPU", "GPU", "RAM", "SSD", "HDD", "MB", "PSU", "CASE" };
            var result = new List<CategoryData>();

            foreach (var type in categoryTypes)
            {
                var component = await _context.Components
                    .Where(c => c.Type == type && c.Quantity > 0)
                    .OrderByDescending(c => c.Id)
                    .Select(c => new ComponentData
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Price = c.Price,
                        Quantity = c.Quantity,
                        Type = c.Type
                    })
                    .FirstOrDefaultAsync();

                result.Add(new CategoryData
                {
                    Type = type,
                    Component = component
                });
            }

            return result;
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

        // HomeController.cs - добавьте этот метод
        [AllowAnonymous]
        public async Task<IActionResult> GetBestsellers(string type = "all", int limit = 6)
        {
            IQueryable<Component> query = _context.Components
                .Where(c => c.Quantity > 0);

            // Фильтрация по типу
            if (!string.IsNullOrEmpty(type) && type.ToLower() != "all")
            {
                query = query.Where(c => c.Type == type.ToUpper());
            }

            // Для "всех категорий" показываем наиболее популярные товары
            // Можно добавить логику сортировки по популярности (количеству продаж)
            // Пока используем сортировку по ID (новые первыми)
            var components = await query
                .OrderByDescending(c => c.Id)
                .Take(limit)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    price = c.Price,
                    type = c.Type,
                    description = c.Description,
                    quantity = c.Quantity,
                    specifications = c.Specifications,
                    socket = c.Socket,
                    memoryType = c.MemoryType,
                    formFactor = c.FormFactor,
                    imageUrl = c.ImageUrl,
                    shortName = c.Name // Для отображения короткого имени
                })
                .ToListAsync();

            return Json(new { success = true, components });
        }

        // Вспомогательный метод для сокращения имени
        private string TruncateName(string name, int maxLength = 30)
        {
            if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
                return name;

            return name.Substring(0, maxLength - 3) + "...";
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
    }


}
