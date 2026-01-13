using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;
using WebShopV3.Json.Models.Recommendation;

namespace WebShopV3.Controllers
{
    public class RecommendationController : Controller
    {
        private readonly IRecommendationService _recommendationService;
        private readonly JsonDataService _jsonData;
        private readonly ILogger<RecommendationController> _logger;

        public RecommendationController(
            IRecommendationService recommendationService,
            JsonDataService jsonData,
            ILogger<RecommendationController> logger)
        {
            _recommendationService = recommendationService;
            _jsonData = jsonData;
            _logger = logger;
        }

        // GET: /Recommendation/ForYou - Персональные рекомендации
        [Authorize]
        public async Task<IActionResult> ForYou(int limit = 12)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var recommendations = await _recommendationService.GetPersonalRecommendationsAsync(userId, limit);

                var viewModel = await GetRecommendationViewModelAsync(recommendations);
                viewModel.Title = "Персональные рекомендации";
                viewModel.Description = "Подборка товаров специально для вас";

                return View("Recommendations", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting personal recommendations");
                TempData["ErrorMessage"] = "Не удалось загрузить рекомендации";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /Recommendation/Similar/{productType}/{productId} - Похожие товары
        public async Task<IActionResult> Similar(string productType, int productId, int limit = 6)
        {
            try
            {
                if (string.IsNullOrEmpty(productType) || productId <= 0)
                {
                    TempData["ErrorMessage"] = "Неверные параметры";
                    return RedirectToAction("Index", "Home");
                }

                var recommendations = await _recommendationService.GetSimilarProductsAsync(productId, productType, limit);

                // Получаем информацию о текущем товаре для заголовка
                string productName = "";
                if (productType == "Computer")
                {
                    var computer = await _jsonData.GetByIdAsync<Computer>("Computers", productId);
                    productName = computer?.Name ?? "Компьютер";
                }
                else if (productType == "Component")
                {
                    var component = await _jsonData.GetByIdAsync<Component>("Components", productId);
                    productName = component?.Name ?? "Комплектующее";
                }

                var viewModel = await GetRecommendationViewModelAsync(recommendations);
                viewModel.Title = $"Похожие на {productName}";
                viewModel.Description = $"Товары, похожие на выбранный вами";
                viewModel.CurrentProductId = productId;
                viewModel.CurrentProductType = productType;

                return View("Recommendations", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting similar products");
                TempData["ErrorMessage"] = "Не удалось загрузить похожие товары";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /Recommendation/Popular - Популярные товары
        public async Task<IActionResult> Popular(string? type = null, int limit = 12)
        {
            try
            {
                var recommendations = await GetPopularItemsFromJson(type, limit);

                var viewModel = await GetRecommendationViewModelAsync(recommendations);
                viewModel.Title = type switch
                {
                    "Computer" => "Популярные компьютеры",
                    "Component" => "Популярные комплектующие",
                    _ => "Популярные товары"
                };
                viewModel.Description = "Самые востребованные товары в нашем магазине";
                viewModel.FilterType = type;

                return View("Recommendations", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular items");
                TempData["ErrorMessage"] = "Не удалось загрузить популярные товары";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /Recommendation/ForCart - Рекомендации для корзины
        [Authorize]
        public async Task<IActionResult> ForCart(int limit = 8)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                // Получаем корзину из сессии
                var cartJson = HttpContext.Session.GetString("Cart");
                if (string.IsNullOrEmpty(cartJson))
                {
                    TempData["InfoMessage"] = "Ваша корзина пуста";
                    return RedirectToAction("Index", "Cart");
                }

                var cart = JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

                if (!cart.Items.Any())
                {
                    TempData["InfoMessage"] = "Ваша корзина пуста";
                    return RedirectToAction("Index", "Cart");
                }

                // Получаем рекомендации на основе товаров в корзине
                var recommendations = await GetCartRecommendationsFromJson(cart, userId, limit);

                var viewModel = await GetRecommendationViewModelAsync(recommendations);
                viewModel.Title = "Дополните вашу покупку";
                viewModel.Description = "Товары, которые часто покупают вместе с выбранными";
                viewModel.ShowCartInfo = true;
                viewModel.CartTotal = cart.TotalAmount;
                viewModel.CartItemCount = cart.TotalItems;

                return View("CartRecommendations", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart recommendations");
                TempData["ErrorMessage"] = "Не удалось загрузить рекомендации для корзины";
                return RedirectToAction("Index", "Cart");
            }
        }

        // GET: /Recommendation/Components - Рекомендации комплектующих по категориям
        public async Task<IActionResult> Components(string? category = null, int limit = 8)
        {
            try
            {
                // Получаем доступные категории компонентов
                var components = await _jsonData.GetAllAsync<Component>("Components");
                var categories = components
                    .Where(c => c.Quantity > 0 && !string.IsNullOrEmpty(c.Type))
                    .Select(c => c.Type!)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();

                // Если категория не указана, берем первую
                if (string.IsNullOrEmpty(category) && categories.Any())
                {
                    category = categories.First();
                }

                List<RecommendationItem> recommendations;

                if (!string.IsNullOrEmpty(category))
                {
                    // Рекомендации для конкретной категории
                    recommendations = await GetPopularComponentsByCategoryFromJson(category, limit);
                }
                else
                {
                    // Общие популярные компоненты
                    recommendations = await GetPopularItemsFromJson("Component", limit);
                }

                var viewModel = await GetRecommendationViewModelAsync(recommendations);
                viewModel.Title = category != null ? $"Рекомендуемые {GetCategoryName(category)}" : "Рекомендуемые комплектующие";
                viewModel.Description = "Лучшие комплектующие для сборки вашего ПК";
                viewModel.Categories = categories;
                viewModel.SelectedCategory = category;

                return View("ComponentRecommendations", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting component recommendations");
                TempData["ErrorMessage"] = "Не удалось загрузить рекомендации комплектующих";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /Recommendation/Sidebar - Частичное представление для сайдбара
        public async Task<IActionResult> Sidebar(int? productId = null, string? productType = null, int limit = 4)
        {
            try
            {
                var userId = User.Identity?.IsAuthenticated == true
                    ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value)
                    : (int?)null;

                var guestId = HttpContext.Request.Cookies["GuestId"] ?? HttpContext.Session.Id;

                // Получаем рекомендации на основе истории просмотров и поведения
                var recommendations = await GetSidebarRecommendationsFromJson(userId, guestId, productId, productType, limit);

                var viewModel = await GetRecommendationViewModelAsync(recommendations);
                viewModel.IsSidebar = true;

                return PartialView("_SidebarRecommendations", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sidebar recommendations");
                return PartialView("_SidebarRecommendations", new RecommendationViewModel());
            }
        }

        // GET: /Recommendation/QuickView - Быстрый просмотр рекомендаций (AJAX)
        public async Task<IActionResult> QuickView(string type = "popular", int limit = 6)
        {
            try
            {
                List<RecommendationItem> recommendations;

                switch (type.ToLower())
                {
                    case "personal" when User.Identity?.IsAuthenticated == true:
                        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                        recommendations = await GetPersonalRecommendationsFromJson(userId, limit);
                        break;

                    case "guest":
                        var guestId = HttpContext.Request.Cookies["GuestId"] ?? HttpContext.Session.Id;
                        recommendations = await GetGuestRecommendationsFromJson(guestId, limit);
                        break;

                    case "cart" when User.Identity?.IsAuthenticated == true:
                        var cartJson = HttpContext.Session.GetString("Cart");
                        if (!string.IsNullOrEmpty(cartJson))
                        {
                            var cart = JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();
                            var cartUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                            recommendations = await GetCartRecommendationsFromJson(cart, cartUserId, limit);
                        }
                        else
                        {
                            recommendations = await GetPopularItemsFromJson(null, limit);
                        }
                        break;

                    default:
                        recommendations = await GetPopularItemsFromJson(null, limit);
                        break;
                }

                var viewModel = await GetRecommendationViewModelAsync(recommendations);
                viewModel.Title = type switch
                {
                    "personal" => "Для вас",
                    "guest" => "Рекомендуем",
                    "cart" => "С этим покупают",
                    _ => "Популярное"
                };

                return PartialView("_QuickRecommendations", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in QuickView");
                return PartialView("_QuickRecommendations", new RecommendationViewModel());
            }
        }

        // Вспомогательные методы

        private async Task<RecommendationViewModel> GetRecommendationViewModelAsync(List<RecommendationItem> recommendations)
        {
            var viewModel = new RecommendationViewModel
            {
                Recommendations = recommendations,
                Count = recommendations.Count
            };

            // Получаем полные данные о каждом товаре
            foreach (var item in recommendations)
            {
                if (item.ProductType == "Computer")
                {
                    var computer = await _jsonData.GetByIdAsync<Computer>("Computers", item.Id);
                    if (computer != null)
                    {
                        item.Name = computer.Name;
                        item.Price = computer.Price;
                        item.ImageUrl = computer.ImageUrl;
                        item.Description = computer.Description;
                        item.StockQuantity = computer.Quantity;
                    }
                }
                else if (item.ProductType == "Component")
                {
                    var component = await _jsonData.GetByIdAsync<Component>("Components", item.Id);
                    if (component != null)
                    {
                        item.Name = component.Name;
                        item.Price = component.Price;
                        item.ImageUrl = component.ImageUrl;
                        item.Description = component.Description;
                        item.StockQuantity = component.Quantity;
                        item.ComponentType = component.Type;
                    }
                }
            }

            // Удаляем товары, которые не найдены или нет в наличии
            viewModel.Recommendations = viewModel.Recommendations
                .Where(r => r.StockQuantity > 0 && !string.IsNullOrEmpty(r.Name))
                .ToList();

            viewModel.Count = viewModel.Recommendations.Count;

            return viewModel;
        }

        // Методы для работы с JSON (заменяют IRecommendationService)

        private async Task<List<RecommendationItem>> GetPopularItemsFromJson(string? type, int limit)
        {
            var random = new Random();
            var recommendations = new List<RecommendationItem>();

            if (type == null || type == "Computer")
            {
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var availableComputers = computers
                    .Where(c => c.Quantity > 0)
                    .OrderByDescending(c => c.Id) // Новые первыми
                    .Take(limit * 2)
                    .ToList();

                foreach (var computer in availableComputers.OrderBy(x => random.Next()).Take(limit))
                {
                    recommendations.Add(new RecommendationItem
                    {
                        Id = computer.Id,
                        ProductType = "Computer",
                        Name = computer.Name,
                        Price = computer.Price,
                        ImageUrl = computer.ImageUrl,
                        Description = computer.Description,
                        StockQuantity = computer.Quantity,
                        RelevanceScore = 0.7 + (random.NextDouble() * 0.3),
                        RecommendationType = "popular"
                    });
                }
            }

            if (type == null || type == "Component")
            {
                var components = await _jsonData.GetAllAsync<Component>("Components");
                var availableComponents = components
                    .Where(c => c.Quantity > 0)
                    .OrderByDescending(c => c.Id)
                    .Take(limit * 2)
                    .ToList();

                foreach (var component in availableComponents.OrderBy(x => random.Next()).Take(limit))
                {
                    recommendations.Add(new RecommendationItem
                    {
                        Id = component.Id,
                        ProductType = "Component",
                        ComponentType = component.Type,
                        Name = component.Name,
                        Price = component.Price,
                        ImageUrl = component.ImageUrl,
                        Description = component.Description,
                        StockQuantity = component.Quantity,
                        RelevanceScore = 0.6 + (random.NextDouble() * 0.4),
                        RecommendationType = "popular"
                    });
                }
            }

            return recommendations
                .OrderByDescending(r => r.RelevanceScore)
                .Take(limit)
                .ToList();
        }

        private async Task<List<RecommendationItem>> GetPopularComponentsByCategoryFromJson(string category, int limit)
        {
            var components = await _jsonData.GetAllAsync<Component>("Components");
            var categoryComponents = components
                .Where(c => c.Type == category && c.Quantity > 0)
                .OrderByDescending(c => c.Id)
                .Take(limit * 2)
                .ToList();

            var random = new Random();
            return categoryComponents
                .OrderBy(x => random.Next())
                .Take(limit)
                .Select(c => new RecommendationItem
                {
                    Id = c.Id,
                    ProductType = "Component",
                    ComponentType = c.Type,
                    Name = c.Name,
                    Price = c.Price,
                    ImageUrl = c.ImageUrl,
                    Description = c.Description,
                    StockQuantity = c.Quantity,
                    RelevanceScore = 0.7 + (random.NextDouble() * 0.3),
                    RecommendationType = "category"
                })
                .ToList();
        }

        private async Task<List<RecommendationItem>> GetCartRecommendationsFromJson(Cart cart, int userId, int limit)
        {
            var recommendations = new List<RecommendationItem>();
            var random = new Random();

            // Получаем типы товаров в корзине
            var cartComponentTypes = cart.Items
                .Where(i => i.IsComponent)
                .Select(i => GetComponentTypeFromCartItem(i))
                .Distinct()
                .ToList();

            // Получаем компьютеры в корзине
            var hasComputersInCart = cart.Items.Any(i => i.IsComputer);

            // Если есть компьютеры в корзине, предлагаем похожие компьютеры
            if (hasComputersInCart)
            {
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var availableComputers = computers
                    .Where(c => c.Quantity > 0)
                    .OrderBy(c => random.Next())
                    .Take(limit / 2)
                    .ToList();

                recommendations.AddRange(availableComputers.Select(c => new RecommendationItem
                {
                    Id = c.Id,
                    ProductType = "Computer",
                    Name = c.Name,
                    Price = c.Price,
                    ImageUrl = c.ImageUrl,
                    StockQuantity = c.Quantity,
                    RelevanceScore = 0.8,
                    RecommendationType = "cart"
                }));
            }

            // Если есть компоненты в корзине, предлагаем дополняющие компоненты
            if (cartComponentTypes.Any())
            {
                var components = await _jsonData.GetAllAsync<Component>("Components");

                foreach (var type in cartComponentTypes)
                {
                    var similarComponents = components
                        .Where(c => c.Type == type && c.Quantity > 0)
                        .OrderBy(c => random.Next())
                        .Take(2)
                        .ToList();

                    recommendations.AddRange(similarComponents.Select(c => new RecommendationItem
                    {
                        Id = c.Id,
                        ProductType = "Component",
                        ComponentType = c.Type,
                        Name = c.Name,
                        Price = c.Price,
                        ImageUrl = c.ImageUrl,
                        StockQuantity = c.Quantity,
                        RelevanceScore = 0.9,
                        RecommendationType = "cart_complement"
                    }));
                }
            }

            // Если рекомендаций мало, добавляем популярные товары
            if (recommendations.Count < limit)
            {
                var popularItems = await GetPopularItemsFromJson(null, limit - recommendations.Count);
                recommendations.AddRange(popularItems);
            }

            return recommendations.Take(limit).ToList();
        }

        private async Task<List<RecommendationItem>> GetPersonalRecommendationsFromJson(int userId, int limit)
        {
            // В реальной системе здесь должна быть сложная логика рекомендаций
            // Сейчас возвращаем популярные товары
            return await GetPopularItemsFromJson(null, limit);
        }

        private async Task<List<RecommendationItem>> GetGuestRecommendationsFromJson(string guestId, int limit)
        {
            // Для гостей возвращаем популярные товары
            return await GetPopularItemsFromJson(null, limit);
        }

        private async Task<List<RecommendationItem>> GetSidebarRecommendationsFromJson(
            int? userId, string guestId, int? productId, string? productType, int limit)
        {
            var recommendations = new List<RecommendationItem>();

            // Если есть текущий товар, предлагаем похожие
            if (productId.HasValue && !string.IsNullOrEmpty(productType))
            {
                if (productType == "Computer")
                {
                    var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                    var similarComputers = computers
                        .Where(c => c.Id != productId.Value && c.Quantity > 0)
                        .OrderBy(c => Guid.NewGuid())
                        .Take(limit)
                        .Select(c => new RecommendationItem
                        {
                            Id = c.Id,
                            ProductType = "Computer",
                            Name = c.Name,
                            Price = c.Price,
                            ImageUrl = c.ImageUrl,
                            StockQuantity = c.Quantity,
                            RelevanceScore = 0.8
                        })
                        .ToList();

                    recommendations.AddRange(similarComputers);
                }
                else if (productType == "Component")
                {
                    var currentComponent = await _jsonData.GetByIdAsync<Component>("Components", productId.Value);
                    if (currentComponent != null)
                    {
                        var components = await _jsonData.GetAllAsync<Component>("Components");
                        var similarComponents = components
                            .Where(c => c.Id != productId.Value &&
                                       c.Type == currentComponent.Type &&
                                       c.Quantity > 0)
                            .OrderBy(c => Guid.NewGuid())
                            .Take(limit)
                            .Select(c => new RecommendationItem
                            {
                                Id = c.Id,
                                ProductType = "Component",
                                ComponentType = c.Type,
                                Name = c.Name,
                                Price = c.Price,
                                ImageUrl = c.ImageUrl,
                                StockQuantity = c.Quantity,
                                RelevanceScore = 0.8
                            })
                            .ToList();

                        recommendations.AddRange(similarComponents);
                    }
                }
            }

            // Если рекомендаций мало, добавляем популярные товары
            if (recommendations.Count < limit)
            {
                var popularItems = await GetPopularItemsFromJson(null, limit - recommendations.Count);
                recommendations.AddRange(popularItems);
            }

            return recommendations.Take(limit).ToList();
        }

        private string? GetComponentTypeFromCartItem(CartItem item)
        {
            // Это упрощенная логика - в реальном приложении нужно получать тип из базы
            return "CPU"; // Пример, нужно реализовать получение реального типа
        }

        private string GetCategoryName(string categoryCode)
        {
            return categoryCode switch
            {
                "CPU" => "процессоры",
                "GPU" => "видеокарты",
                "RAM" => "оперативная память",
                "SSD" => "SSD накопители",
                "HDD" => "жесткие диски",
                "MB" => "материнские платы",
                "PSU" => "блоки питания",
                "CASE" => "корпуса",
                "Cooler" => "кулеры",
                "Monitor" => "мониторы",
                _ => categoryCode
            };
        }
    }

    // ViewModel для рекомендаций (без изменений)
    public class RecommendationViewModel
    {
        public List<RecommendationItem> Recommendations { get; set; } = new();
        public string Title { get; set; } = "Рекомендации";
        public string Description { get; set; } = string.Empty;
        public int Count { get; set; }

        // Для фильтрации
        public string? FilterType { get; set; }
        public string? SelectedCategory { get; set; }
        public List<string>? Categories { get; set; }

        // Для контекста
        public int? CurrentProductId { get; set; }
        public string? CurrentProductType { get; set; }

        // Для корзины
        public bool ShowCartInfo { get; set; }
        public decimal CartTotal { get; set; }
        public int CartItemCount { get; set; }

        // Для сайдбара
        public bool IsSidebar { get; set; }
    }
}