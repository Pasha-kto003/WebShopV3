using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebShopV3.Json.Helpers;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;
using WebShopV3.Json.Models.DTO;

namespace WebShopV3.Json.Controllers
{
    public class HomeController : Controller
    {
        private readonly JsonDataService _jsonData;
        private readonly IRecommendationService _recommendationService;
        private const string RecentlyViewedCookieName = "WebShopV3_RecentlyViewed";

        public HomeController(JsonDataService jsonData, IRecommendationService recommendationService)
        {
            _jsonData = jsonData;
            _recommendationService = recommendationService;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            computers = computers
                .Where(c => c.Quantity > 0)
                .Take(8)
                .ToList();

            var components = await _jsonData.GetAllAsync<Component>("Components");
            var bestsellers = components
                .Where(c => c.Quantity > 0)
                .OrderByDescending(c => c.Id)
                .Take(6)
                .ToList();

            // Получаем статистику для View
            ViewBag.TotalComponents = components.Count(c => c.Quantity > 0);
            ViewBag.TotalTypes = components
                .Select(c => c.Type)
                .Distinct()
                .Count();

            // Получаем компоненты по категориям
            var categoryData = await GetComponentCategoriesDataAsync();
            ViewBag.ComponentCategoriesData = categoryData;

            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;
            ViewBag.Bestsellers = bestsellers;
            ViewBag.ComponentTypes = components
                .Select(c => c.Type)
                .Distinct()
                .Where(t => !string.IsNullOrEmpty(t))
                .Take(8)
                .ToList();

            return View(computers);
        }

        [AllowAnonymous]
        public async Task<IActionResult> ComponentDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var component = await GetComponentWithCharacteristics(id.Value);
            if (component == null)
            {
                return NotFound();
            }

            // Получаем связанные компоненты (того же типа)
            var allComponents = await _jsonData.GetAllAsync<Component>("Components");
            var relatedComponents = allComponents
                .Where(c => c.Type == component.Type && c.Id != component.Id && c.Quantity > 0)
                .OrderByDescending(c => c.Id)
                .Take(4)
                .ToList();

            // Передаем данные через ViewBag и ViewData
            ViewBag.RelatedComponents = relatedComponents;
            ViewBag.ComponentCharacteristics = component.ComponentCharacteristics?.ToList() ?? new List<ComponentCharacteristic>();

            var recentlyViewed = GetRecentlyViewedComputers();

            ViewBag.RecentlyViewed = recentlyViewed.Result.Where(c => c.Id != id.Value).Take(5).ToList();

            var componentTypes = allComponents
                .Select(cc => cc.Type)
                .Distinct()
                .ToList();

            var allComputers = await _jsonData.GetAllAsync<Computer>("Computers");
            var recommendedComputers = allComputers
                .Where(c => c.Quantity > 0 && c.Id != id)
                .Take(6)
                .ToList();

            var recommendedComponents = allComponents
                .Where(c => c.Quantity > 0 && componentTypes.Contains(c.Type ?? ""))
                .Take(6)
                .ToList();

            // Передаем через ViewBag
            ViewBag.RecommendedComputers = recommendedComputers;
            ViewBag.RecommendedComponents = recommendedComponents;

            // Получаем корзину для отображения количества
            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);
            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;

            // Трекинг просмотра
            await Helpers.RecommendationHelper.TrackViewAsync(
                HttpContext,
                _recommendationService,
                component.Id,
                "Component",
                component.Name);

            // Получение рекомендаций для сайдбара
            var sidebarRecommendations = await Helpers.RecommendationHelper.GetSidebarRecommendationsAsync(
                HttpContext,
                _recommendationService,
                component.Id,
                "Component",
                4);

            ViewBag.SidebarRecommendations = sidebarRecommendations;

            return View(component);
        }

        private async Task<List<CategoryData>> GetComponentCategoriesDataAsync()
        {
            var categoryTypes = new[] { "CPU", "GPU", "RAM", "SSD", "HDD", "MB", "PSU", "CASE" };
            var result = new List<CategoryData>();

            var allComponents = await _jsonData.GetAllAsync<Component>("Components");

            foreach (var type in categoryTypes)
            {
                var component = allComponents
                    .Where(c => c.Type == type && c.Quantity > 0)
                    .OrderByDescending(c => c.Id)
                    .Select(c => new ComponentData
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Price = c.Price,
                        Quantity = c.Quantity,
                        Type = c.Type,
                        ImageUrl = c.ImageUrl
                    })
                    .FirstOrDefault();

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

            var computer = await GetComputerWithDetails(id.Value);
            if (computer == null) return NotFound();

            AddToRecentlyViewed(computer.Id, computer.Name, computer.Price, computer.ImageUrl);

            var recentlyViewed = GetRecentlyViewedComputers();

            ViewBag.RecentlyViewed = recentlyViewed.Result
                .Where(c => c.Id != id.Value)
                .Take(5)
                .ToList();

            var componentTypes = computer.ComputerComponents
                .Select(cc => cc.Component?.Type)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();

            var allComputers = await _jsonData.GetAllAsync<Computer>("Computers");
            var recommendedComputers = allComputers
                .Where(c => c.Quantity > 0 && c.Id != id)
                .Take(6)
                .ToList();

            var allComponents = await _jsonData.GetAllAsync<Component>("Components");
            var recommendedComponents = allComponents
                .Where(c => c.Quantity > 0 && componentTypes.Contains(c.Type))
                .Take(6)
                .ToList();

            // Передаем через ViewBag
            ViewBag.RecommendedComputers = recommendedComputers;
            ViewBag.RecommendedComponents = recommendedComponents;

            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;
            ViewBag.CurrentComputerId = id.Value;
            ViewBag.RecentlyViewedCount = recentlyViewed.Result.Count;

            // Трекинг просмотра
            await Helpers.RecommendationHelper.TrackViewAsync(
                HttpContext,
                _recommendationService,
                computer.Id,
                "Computer",
                computer.Name);

            // Получение рекомендаций для сайдбара
            var sidebarRecommendations = await Helpers.RecommendationHelper.GetSidebarRecommendationsAsync(
                HttpContext,
                _recommendationService,
                computer.Id,
                "Computer",
                4);

            ViewBag.SidebarRecommendations = sidebarRecommendations;

            return View(computer);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Catalog(string search = null, string sortBy = "default", string componentType = "all", decimal? minPrice = null, decimal? maxPrice = null, string productType = "all")
        {
            ViewBag.SearchQuery = search;
            ViewBag.SortBy = sortBy;
            ViewBag.ComponentType = componentType;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.ProductType = productType;

            var allComponents = await _jsonData.GetAllAsync<Component>("Components");
            ViewBag.ComponentTypes = allComponents
                .Select(c => c.Type)
                .Distinct()
                .ToList();

            var computers = productType == "components"
                ? new List<Computer>()
                : await GetFilteredComputers(search, sortBy, componentType, minPrice, maxPrice);

            var components = productType == "computers"
                ? new List<Component>()
                : await GetFilteredComponents(search, sortBy, componentType, minPrice, maxPrice);

            // ЗАГРУЖАЕМ СВЯЗАННЫЕ КОМПОНЕНТЫ ДЛЯ КАЖДОГО КОМПЬЮТЕРА
            await LoadComputerComponents(computers);

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
            ViewBag.TotalCount = computers.Count + components.Count;

            return View(viewModel);
        }

        // Метод для загрузки компонентов компьютеров
        private async Task LoadComputerComponents(List<Computer> computers)
        {
            if (!computers.Any())
                return;

            try
            {
                // Загружаем связи
                var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
                var allComponents = await _jsonData.GetAllAsync<Component>("Components");

                // Группируем связи по ID компьютера
                var componentsByComputerId = computerComponents
                    .GroupBy(cc => cc.ComputerId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(cc => cc.ComponentId).ToList()
                    );

                // Для каждого компьютера добавляем связанные компоненты
                foreach (var computer in computers)
                {
                    if (componentsByComputerId.TryGetValue(computer.Id, out var componentIds))
                    {
                        // Получаем компоненты по ID
                        computer.ComputerComponents = allComponents
                            .Where(c => componentIds.Contains(c.Id))
                            .Select(c => new ComputerComponent
                            {
                                ComputerId = computer.Id,
                                ComponentId = c.Id,
                                Component = c
                            })
                            .ToList();
                    }
                    else
                    {
                        computer.ComputerComponents = new List<ComputerComponent>();
                    }
                }
            }
            catch (Exception ex)
            {
               
            }
        }

        // Метод для фильтрации компьютеров
        private async Task<List<Computer>> GetFilteredComputers(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice)
        {
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");

            // Применяем фильтры
            var filteredComputers = computers.Where(c => c.Quantity > 0);

            // Фильтр по поиску
            if (!string.IsNullOrEmpty(search))
            {
                filteredComputers = filteredComputers.Where(c =>
                    c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            // Фильтр по цене
            if (minPrice.HasValue)
                filteredComputers = filteredComputers.Where(c => c.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                filteredComputers = filteredComputers.Where(c => c.Price <= maxPrice.Value);

            // Фильтр по типу компонентов (если тип указан)
            if (componentType != "all")
            {
                // Сначала получим компьютеры, у которых есть компоненты такого типа
                var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
                var allComponents = await _jsonData.GetAllAsync<Component>("Components");

                // Находим ID компьютеров, содержащих компоненты заданного типа
                var computerIdsWithComponentType = computerComponents
                    .Where(cc => allComponents.Any(c => c.Id == cc.ComponentId && c.Type == componentType))
                    .Select(cc => cc.ComputerId)
                    .Distinct()
                    .ToList();

                filteredComputers = filteredComputers.Where(c => computerIdsWithComponentType.Contains(c.Id));
            }

            // Сортировка
            filteredComputers = sortBy switch
            {
                "price_asc" => filteredComputers.OrderBy(c => c.Price),
                "price_desc" => filteredComputers.OrderByDescending(c => c.Price),
                "name_asc" => filteredComputers.OrderBy(c => c.Name),
                "name_desc" => filteredComputers.OrderByDescending(c => c.Name),
                "newest" => filteredComputers.OrderByDescending(c => c.Id),
                _ => filteredComputers.OrderByDescending(c => c.Id) // default
            };

            return filteredComputers.ToList();
        }

        // Метод для фильтрации компонентов
        private async Task<List<Component>> GetFilteredComponents(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice)
        {
            var components = await _jsonData.GetAllAsync<Component>("Components");

            // Применяем фильтры
            var filteredComponents = components.Where(c => c.Quantity > 0);

            // Фильтр по типу
            if (componentType != "all")
            {
                filteredComponents = filteredComponents.Where(c => c.Type == componentType);
            }

            // Фильтр по поиску
            if (!string.IsNullOrEmpty(search))
            {
                filteredComponents = filteredComponents.Where(c =>
                    c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            // Фильтр по цене
            if (minPrice.HasValue)
                filteredComponents = filteredComponents.Where(c => c.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                filteredComponents = filteredComponents.Where(c => c.Price <= maxPrice.Value);

            // Сортировка
            filteredComponents = sortBy switch
            {
                "price_asc" => filteredComponents.OrderBy(c => c.Price),
                "price_desc" => filteredComponents.OrderByDescending(c => c.Price),
                "name_asc" => filteredComponents.OrderBy(c => c.Name),
                "name_desc" => filteredComponents.OrderByDescending(c => c.Name),
                "newest" => filteredComponents.OrderByDescending(c => c.Id),
                _ => filteredComponents.OrderByDescending(c => c.Id) // default
            };

            return filteredComponents.ToList();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SearchProducts(
            string search,
            string sortBy = "default",
            string componentType = "all",
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string productType = "all",
            string viewMode = "cards",
            int page = 1,
            int pageSize = 12)
        {
            try
            {
                // Получаем компьютеры и комплектующие
                var computers = productType == "components"
                    ? new List<Computer>()
                    : await GetFilteredComputers(search, sortBy, componentType, minPrice, maxPrice);

                var components = productType == "computers"
                    ? new List<Models.Component>()
                    : await GetFilteredComponents(search, sortBy, componentType, minPrice, maxPrice);

                // Применяем сортировку
                if (sortBy == "price_asc")
                {
                    computers = computers.OrderBy(c => c.Price).ToList();
                    components = components.OrderBy(c => c.Price).ToList();
                }
                else if (sortBy == "price_desc")
                {
                    computers = computers.OrderByDescending(c => c.Price).ToList();
                    components = components.OrderByDescending(c => c.Price).ToList();
                }
                else if (sortBy == "name_asc")
                {
                    computers = computers.OrderBy(c => c.Name).ToList();
                    components = components.OrderBy(c => c.Name).ToList();
                }
                else if (sortBy == "name_desc")
                {
                    computers = computers.OrderByDescending(c => c.Name).ToList();
                    components = components.OrderByDescending(c => c.Name).ToList();
                }
                else if (sortBy == "newest")
                {
                    computers = computers.OrderByDescending(c => c.Id).ToList();
                    components = components.OrderByDescending(c => c.Id).ToList();
                }

                var viewModel = new CatalogViewModel
                {
                    Computers = computers,
                    Components = components,
                    ProductType = productType
                };

                var totalItems = computers.Count + components.Count;
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                // Пагинация
                if (page > 1)
                {
                    var skip = (page - 1) * pageSize;
                    computers = computers.Skip(skip).Take(pageSize).ToList();
                    components = components.Skip(skip).Take(pageSize).ToList();
                }
                else
                {
                    computers = computers.Take(pageSize).ToList();
                    components = components.Take(pageSize).ToList();
                }

                ViewBag.SearchQuery = search;
                ViewBag.SortBy = sortBy;
                ViewBag.ComponentType = componentType;
                ViewBag.MinPrice = minPrice;
                ViewBag.MaxPrice = maxPrice;
                ViewBag.ProductType = productType;
                ViewBag.ViewMode = viewMode;
                ViewBag.TotalCount = computers.Count + components.Count;
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalItems = totalItems;
                ViewBag.TotalPages = totalPages;

                // Проверяем AJAX запрос
                var isAjax = Request.Headers["X-Requested-With"].Contains("XMLHttpRequest");
                if (isAjax)
                {
                    return PartialView("_ComputerListPartial", viewModel);
                }

                return View("Catalog", viewModel);
            }
            catch (Exception ex)
            {
                // Для AJAX запросов возвращаем JSON с ошибкой
                if (Request.Headers["X-Requested-With"].Contains("XMLHttpRequest"))
                {
                    Response.StatusCode = 500;
                    return Json(new { error = ex.Message });
                }

                // Для обычных запросов возвращаем пустую модель
                var emptyModel = new CatalogViewModel
                {
                    Computers = new List<Computer>(),
                    Components = new List<Models.Component>(),
                    ProductType = productType
                };

                return View("Catalog", emptyModel);
            }
        }

        private class RecentlyViewedItem
        {
            public int ComputerId { get; set; }
            public string ComputerName { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string? ImageUrl { get; set; }
            public DateTime ViewedAt { get; set; }
        }

        private void AddToRecentlyViewed(int computerId, string computerName, decimal price, string? imageUrl)
        {
            try
            {
                var history = GetRecentlyViewedFromCookie();

                // Проверяем, не просматривалось ли недавно (в течение 2 часов)
                var existing = history.FirstOrDefault(x => x.ComputerId == computerId);
                if (existing != null && (DateTime.UtcNow - existing.ViewedAt).TotalHours < 2)
                {
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

        private async Task<List<Computer>> GetRecentlyViewedComputers()
        {
            var recentlyViewedItems = GetRecentlyViewedFromCookie();
            if (!recentlyViewedItems.Any())
                return new List<Computer>();

            var computerIds = recentlyViewedItems.Select(x => x.ComputerId).ToList();
            var allComputers = await _jsonData.GetAllAsync<Computer>("Computers");
            var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
            var allComponents = await _jsonData.GetAllAsync<Component>("Components");

            var computers = allComputers
                .Where(c => computerIds.Contains(c.Id))
                .ToList();

            // Загружаем компоненты для компьютеров
            foreach (var computer in computers)
            {
                var componentIds = computerComponents
                    .Where(cc => cc.ComputerId == computer.Id)
                    .Select(cc => cc.ComponentId)
                    .ToList();

                computer.ComputerComponents = allComponents
                    .Where(c => componentIds.Contains(c.Id))
                    .Select(c => new ComputerComponent
                    {
                        ComputerId = computer.Id,
                        ComponentId = c.Id,
                        Component = c
                    })
                    .ToList();
            }

            // Сохраняем порядок из куки
            return computers
                .OrderBy(c => computerIds.IndexOf(c.Id))
                .Take(6)
                .ToList();
        }

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

        private void SaveHistoryToCookie(List<RecentlyViewedItem> history)
        {
            var serialized = JsonSerializer.Serialize(history);

            var options = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(45),
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Strict,
                Secure = Request.IsHttps,
                Path = "/"
            };

            Response.Cookies.Append(RecentlyViewedCookieName, serialized, options);
        }

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

            var returnUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private string? TruncateString(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength - 3) + "...";
        }

        [AllowAnonymous]
        public IActionResult GetCompareCount()
        {
            var compareJson = HttpContext.Session.GetString("CompareItems");
            var compareItems = string.IsNullOrEmpty(compareJson)
                ? new List<CompareItem>()
                : JsonSerializer.Deserialize<List<CompareItem>>(compareJson) ?? new List<CompareItem>();

            return Json(new { count = compareItems.Count });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetRecentlyViewedJson()
        {
            var recentlyViewed = await GetRecentlyViewedComputers();
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
                        .FirstOrDefault(comp => comp?.Type == "CPU")?.Name ?? "Не указан",
                    gpu = c.ComputerComponents?
                        .Select(cc => cc.Component)
                        .FirstOrDefault(comp => comp?.Type == "GPU")?.Name ?? "Не указан",
                    url = Url.Action("ComputerDetails", "Home", new { id = c.Id })
                })
                .ToList();

            return Json(new { success = true, data = result });
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetBestsellers(string type = "all", int limit = 6)
        {
            var allComponents = await _jsonData.GetAllAsync<Component>("Components");
            var query = allComponents
                .Where(c => c.Quantity > 0)
                .AsEnumerable();

            // Фильтрация по типу
            if (!string.IsNullOrEmpty(type) && type.ToLower() != "all")
            {
                query = query.Where(c => c.Type == type.ToUpper());
            }

            var components = query
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
                    shortName = c.Name
                })
                .ToList();

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

        // Вспомогательные методы для загрузки данных

        private async Task<Component?> GetComponentWithCharacteristics(int componentId)
        {
            var component = await _jsonData.GetByIdAsync<Component>("Components", componentId);
            if (component == null) return null;

            // Загружаем характеристики компонента
            var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");
            var characteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");

            var characteristicIds = componentCharacteristics
                .Where(cc => cc.ComponentId == componentId)
                .Select(cc => cc.CharacteristicId)
                .ToList();

            var componentChars = characteristics
                .Where(c => characteristicIds.Contains(c.Id))
                .Select(c => new ComponentCharacteristic
                {
                    ComponentId = componentId,
                    CharacteristicId = c.Id,
                    Characteristic = c,
                    Value = componentCharacteristics
                        .FirstOrDefault(cc => cc.ComponentId == componentId && cc.CharacteristicId == c.Id)?
                        .Value ?? ""
                })
                .ToList();

            component.ComponentCharacteristics = componentChars;
            return component;
        }

        private async Task<Computer?> GetComputerWithDetails(int computerId)
        {
            var computer = await _jsonData.GetByIdAsync<Computer>("Computers", computerId);
            if (computer == null) return null;

            // Загружаем компоненты компьютера с их характеристиками
            var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
            var allComponents = await _jsonData.GetAllAsync<Component>("Components");
            var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");
            var characteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");

            // Получаем ID компонентов компьютера
            var componentIds = computerComponents
                .Where(cc => cc.ComputerId == computerId)
                .Select(cc => cc.ComponentId)
                .ToList();

            // Создаем список компонентов с характеристиками
            var componentsList = new List<Component>();
            foreach (var componentId in componentIds)
            {
                var component = allComponents.FirstOrDefault(c => c.Id == componentId);
                if (component == null) continue;

                // Загружаем характеристики для компонента
                var characteristicIds = componentCharacteristics
                    .Where(cc => cc.ComponentId == componentId)
                    .Select(cc => cc.CharacteristicId)
                    .ToList();

                var componentChars = characteristics
                    .Where(c => characteristicIds.Contains(c.Id))
                    .Select(c => new ComponentCharacteristic
                    {
                        ComponentId = componentId,
                        CharacteristicId = c.Id,
                        Characteristic = c,
                        Value = componentCharacteristics
                            .FirstOrDefault(cc => cc.ComponentId == componentId && cc.CharacteristicId == c.Id)?
                            .Value ?? ""
                    })
                    .ToList();

                component.ComponentCharacteristics = componentChars;
                componentsList.Add(component);
            }

            // Создаем коллекцию ComputerComponent
            computer.ComputerComponents = componentsList
                .Select(c => new ComputerComponent
                {
                    ComputerId = computerId,
                    ComponentId = c.Id,
                    Component = c
                })
                .ToList();

            return computer;
        }
    }
}