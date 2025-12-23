// Controllers/RecommendationController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using WebShopV3.Models;
using WebShopV3.Models.Recommendation;
using WebShopV3.Services;

namespace WebShopV3.Controllers
{
    public class RecommendationController : Controller
    {
        private readonly IRecommendationService _recommendationService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RecommendationController> _logger;

        public RecommendationController(
            IRecommendationService recommendationService,
            ApplicationDbContext context,
            ILogger<RecommendationController> logger)
        {
            _recommendationService = recommendationService;
            _context = context;
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

                // Получаем полные данные о товарах
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
                    var computer = await _context.Computers.FindAsync(productId);
                    productName = computer?.Name ?? "Компьютер";
                }
                else if (productType == "Component")
                {
                    var component = await _context.Components.FindAsync(productId);
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
                var recommendations = await _recommendationService.GetPopularItemsAsync(type, limit);

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

                var recommendations = await _recommendationService.GetCartRecommendationsAsync(cart, userId, null);

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
                var categories = await _context.Components
                    .Where(c => c.Quantity > 0 && !string.IsNullOrEmpty(c.Type))
                    .Select(c => c.Type!)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();

                // Если категория не указана, берем первую
                if (string.IsNullOrEmpty(category) && categories.Any())
                {
                    category = categories.First();
                }

                List<RecommendationItem> recommendations;

                if (!string.IsNullOrEmpty(category))
                {
                    // Рекомендации для конкретной категории
                    var popularInCategory = await GetPopularComponentsByCategoryAsync(category, limit);
                    recommendations = popularInCategory;
                }
                else
                {
                    // Общие популярные компоненты
                    recommendations = await _recommendationService.GetPopularItemsAsync("Component", limit);
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

                var request = new RecommendationRequest
                {
                    UserId = userId,
                    GuestId = guestId,
                    CurrentProductId = productId,
                    CurrentProductType = productType,
                    Limit = limit,
                    IncludeComputers = true,
                    IncludeComponents = true
                };

                var recommendations = await _recommendationService.GetRecommendationsAsync(request);

                // Для сайдбара показываем только первые N товаров
                recommendations = recommendations.Take(limit).ToList();

                var viewModel = await GetRecommendationViewModelAsync(recommendations);
                viewModel.IsSidebar = true;

                return PartialView("_SidebarRecommendations", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sidebar recommendations");
                // В случае ошибки возвращаем пустой список
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
                        recommendations = await _recommendationService.GetPersonalRecommendationsAsync(userId, limit);
                        break;

                    case "guest":
                        var guestId = HttpContext.Request.Cookies["GuestId"] ?? HttpContext.Session.Id;
                        recommendations = await _recommendationService.GetGuestRecommendationsAsync(guestId, limit);
                        break;

                    case "cart" when User.Identity?.IsAuthenticated == true:
                        var cartJson = HttpContext.Session.GetString("Cart");
                        if (!string.IsNullOrEmpty(cartJson))
                        {
                            var cart = JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();
                            var cartUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                            recommendations = await _recommendationService.GetCartRecommendationsAsync(
                                cart, cartUserId, null);
                        }
                        else
                        {
                            recommendations = await _recommendationService.GetPopularItemsAsync(null, limit);
                        }
                        break;

                    default:
                        recommendations = await _recommendationService.GetPopularItemsAsync(null, limit);
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
                    var computer = await _context.Computers.FindAsync(item.Id);
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
                    var component = await _context.Components.FindAsync(item.Id);
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

        private async Task<List<RecommendationItem>> GetPopularComponentsByCategoryAsync(string category, int limit)
        {
            var components = await _context.Components
                .Where(c => c.Type == category && c.Quantity > 0)
                .OrderByDescending(c => c.Id) // Новые первыми
                .Take(limit * 2)
                .ToListAsync();

            var random = new Random();
            return components
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

    // ViewModel для рекомендаций
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