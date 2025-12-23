// Services/RecommendationService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using WebShopV3.Models;
using WebShopV3.Models.Recommendation;

namespace WebShopV3.Services
{
    public interface IRecommendationService
    {
        // Отслеживание действий
        void TrackAction(UserAction action);

        // Получение рекомендаций
        Task<List<RecommendationItem>> GetRecommendationsAsync(RecommendationRequest request);

        // Рекомендации для товара
        Task<List<RecommendationItem>> GetSimilarProductsAsync(int productId, string productType, int limit = 5);

        // Рекомендации на основе корзины
        Task<List<RecommendationItem>> GetCartRecommendationsAsync(Cart cart, int? userId, string? guestId);

        // Популярное
        Task<List<RecommendationItem>> GetPopularItemsAsync(string? productType = null, int limit = 10);

        // Для гостя (на основе сессии)
        Task<List<RecommendationItem>> GetGuestRecommendationsAsync(string guestId, int limit = 6);

        // Для пользователя (персональные)
        Task<List<RecommendationItem>> GetPersonalRecommendationsAsync(int userId, int limit = 10);

        // Очистка старых данных
        Task CleanupOldDataAsync();
    }

    public class RecommendationService : IRecommendationService, IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RecommendationService> _logger;
        private readonly CompatibilityService _compatibilityService;

        // In-memory хранилище действий пользователей (временное, можно заменить на Redis)
        private readonly ConcurrentDictionary<string, List<UserAction>> _userActions = new();
        private readonly ConcurrentDictionary<string, List<UserAction>> _guestActions = new();
        private readonly ConcurrentDictionary<int, List<int>> _productAssociations = new();

        // Ключи кэша
        private const string POPULAR_CACHE_KEY = "recommendations_popular_{0}_{1}";
        private const string SIMILAR_CACHE_KEY = "recommendations_similar_{0}_{1}";
        private const string PERSONAL_CACHE_KEY = "recommendations_personal_{0}";
        private const string GUEST_CACHE_KEY = "recommendations_guest_{0}";

        // Настройки
        private readonly TimeSpan _actionLifetime = TimeSpan.FromDays(30);
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);
        private readonly int _maxActionsPerUser = 1000;

        // Веса для алгоритма
        private readonly Dictionary<UserActionType, double> _actionWeights = new()
        {
            { UserActionType.Purchase, 5.0 },
            { UserActionType.AddToCart, 3.0 },
            { UserActionType.AddToFavorite, 2.0 },
            { UserActionType.Compare, 1.5 },
            { UserActionType.View, 1.0 },
            { UserActionType.Search, 0.5 }
        };

        // Время устаревания рекомендаций
        private readonly Dictionary<string, TimeSpan> _recommendationTTL = new()
        {
            { "popular", TimeSpan.FromHours(1) },
            { "similar", TimeSpan.FromMinutes(30) },
            { "personalized", TimeSpan.FromMinutes(10) },
            { "complementary", TimeSpan.FromMinutes(15) }
        };

        public RecommendationService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<RecommendationService> logger,
            CompatibilityService compatibilityService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _compatibilityService = compatibilityService;

            // Запускаем очистку старых данных
            _ = CleanupOldDataPeriodically();
        }

        public void TrackAction(UserAction action)
        {
            try
            {
                var key = action.UserId.HasValue
                    ? $"user_{action.UserId}"
                    : $"guest_{action.GuestId}";

                var storage = action.UserId.HasValue ? _userActions : _guestActions;

                storage.AddOrUpdate(key,
                    new List<UserAction> { action },
                    (key, existingActions) =>
                    {
                        // Добавляем новое действие
                        existingActions.Add(action);

                        // Ограничиваем количество
                        if (existingActions.Count > _maxActionsPerUser)
                        {
                            existingActions = existingActions
                                .OrderByDescending(a => a.Timestamp)
                                .Take(_maxActionsPerUser / 2)
                                .ToList();
                        }

                        return existingActions;
                    });

                // Инвалидируем кэш
                if (action.UserId.HasValue)
                {
                    _cache.Remove(string.Format(PERSONAL_CACHE_KEY, action.UserId));
                }
                else if (!string.IsNullOrEmpty(action.GuestId))
                {
                    _cache.Remove(string.Format(GUEST_CACHE_KEY, action.GuestId));
                }

                _logger.LogDebug("Tracked action: {UserId}/{GuestId} - {ActionType} - {ProductType}:{ProductId}",
                    action.UserId, action.GuestId, action.ActionType, action.ProductType, action.ProductId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking user action");
            }
        }

        public async Task<List<RecommendationItem>> GetRecommendationsAsync(RecommendationRequest request)
        {
            var cacheKey = $"recommendations_{request.UserId}_{request.GuestId}_{request.CurrentProductId}_{request.Limit}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

                var recommendations = new List<RecommendationItem>();

                // 1. Персональные рекомендации (если есть история)
                if (request.UserId.HasValue || !string.IsNullOrEmpty(request.GuestId))
                {
                    var personal = request.UserId.HasValue
                        ? await GetPersonalRecommendationsAsync(request.UserId.Value, request.Limit / 2)
                        : await GetGuestRecommendationsAsync(request.GuestId!, request.Limit / 2);

                    recommendations.AddRange(personal);
                }

                // 2. Рекомендации на основе текущего товара
                if (request.CurrentProductId.HasValue && !string.IsNullOrEmpty(request.CurrentProductType))
                {
                    var similar = await GetSimilarProductsAsync(
                        request.CurrentProductId.Value,
                        request.CurrentProductType,
                        request.Limit / 2);

                    recommendations.AddRange(similar);
                }

                // 3. Популярные товары (заполняем остаток)
                if (recommendations.Count < request.Limit)
                {
                    var needed = request.Limit - recommendations.Count;
                    var popular = await GetPopularItemsAsync(null, needed);
                    recommendations.AddRange(popular);
                }

                // Убираем дубликаты и сортируем по релевантности
                return recommendations
                    .GroupBy(r => new { r.ProductType, r.Id })
                    .Select(g => g.OrderByDescending(r => r.RelevanceScore).First())
                    .OrderByDescending(r => r.RelevanceScore)
                    .Take(request.Limit)
                    .ToList();
            }) ?? new List<RecommendationItem>();
        }

        public async Task<List<RecommendationItem>> GetSimilarProductsAsync(int productId, string productType, int limit = 5)
        {
            var cacheKey = string.Format(SIMILAR_CACHE_KEY, productType, productId);

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _recommendationTTL["similar"];

                var similarItems = new List<RecommendationItem>();

                if (productType == "Computer")
                {
                    similarItems = await FindSimilarComputersAsync(productId, limit);
                }
                else if (productType == "Component")
                {
                    similarItems = await FindSimilarComponentsAsync(productId, limit);
                }

                return similarItems;
            }) ?? new List<RecommendationItem>();
        }

        // В методе FindSimilarComponentsAsync:
        private async Task<List<RecommendationItem>> FindSimilarComponentsAsync(int componentId, int limit)
        {
            var component = await _context.Components
                .Include(c => c.ComponentCharacteristics)
                .FirstOrDefaultAsync(c => c.Id == componentId);

            if (component == null) return new List<RecommendationItem>();

            var componentType = component.Type ?? "";
            var currentPrice = component.Price;
            var currentSpecs = component.Specifications?.ToLower() ?? "";

            var similarComponents = await _context.Components
                .Where(c => c.Id != componentId &&
                           c.Type == componentType &&
                           c.Quantity > 0)
                .ToListAsync();

            var scoredComponents = new List<(Component Component, double Score, Dictionary<string, double> Factors)>();

            foreach (var otherComponent in similarComponents)
            {
                double score = 0;
                var factors = new Dictionary<string, double>();

                // 1. Сходство цены (40%)
                var priceScore = CalculatePriceSimilarity(currentPrice, otherComponent.Price);
                score += priceScore * 0.4;
                factors["price"] = priceScore;

                // 2. Сходство спецификаций (30%)
                if (!string.IsNullOrEmpty(otherComponent.Specifications))
                {
                    var otherSpecs = otherComponent.Specifications.ToLower();
                    var commonTerms = GetCommonTerms(currentSpecs, otherSpecs);
                    var termCount = GetTermCount(currentSpecs);
                    var specScore = termCount > 0 ? (double)commonTerms / termCount : 0;
                    score += specScore * 0.3;
                    factors["specs"] = specScore;
                }

                // 3. Бренд/модель (20%)
                var brandScore = AreSameBrand(component.Name, otherComponent.Name) ? 0.2 : 0;
                score += brandScore;
                factors["brand"] = brandScore;

                // 4. Наличие (10%)
                var stockScore = otherComponent.Quantity > 10 ? 0.1 :
                                otherComponent.Quantity > 3 ? 0.05 : 0;
                score += stockScore;
                factors["stock"] = stockScore;

                scoredComponents.Add((otherComponent, score, factors));
            }

            return scoredComponents
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new RecommendationItem
                {
                    Id = x.Component.Id,
                    ProductType = "Component",
                    ComponentType = x.Component.Type,
                    Name = x.Component.Name,
                    Price = x.Component.Price,
                    ImageUrl = x.Component.ImageUrl,
                    Description = x.Component.Description,
                    StockQuantity = x.Component.Quantity,
                    RelevanceScore = x.Score,
                    RecommendationType = "similar",
                    ScoreFactors = x.Factors // Используем x.Factors из кортежа
                })
                .ToList();
        }

        private async Task<List<RecommendationItem>> FindSimilarComputersAsync(int computerId, int limit)
        {
            var computer = await _context.Computers
                .Include(c => c.ComputerComponents)
                .FirstOrDefaultAsync(c => c.Id == computerId);

            if (computer == null) return new List<RecommendationItem>();

            var currentComponents = computer.ComputerComponents.Select(cc => cc.ComponentId).ToList();
            var currentPrice = computer.Price;
            var currentDescription = computer.Description?.ToLower() ?? "";

            var allComputers = await _context.Computers
                .Include(c => c.ComputerComponents)
                .Where(c => c.Id != computerId && c.Quantity > 0)
                .ToListAsync();

            var scoredComputers = new List<(Computer Computer, double Score, Dictionary<string, double> Factors)>();

            foreach (var otherComputer in allComputers)
            {
                double score = 0;
                var factors = new Dictionary<string, double>();

                // 1. Совпадение компонентов (30%)
                var otherComponents = otherComputer.ComputerComponents.Select(cc => cc.ComponentId).ToList();
                var commonComponents = currentComponents.Intersect(otherComponents).Count();
                var componentScore = (double)commonComponents / Math.Max(currentComponents.Count, 1);
                score += componentScore * 0.3;
                factors["components"] = componentScore;

                // 2. Сходство цены (25%)
                var priceScore = CalculatePriceSimilarity(currentPrice, otherComputer.Price);
                score += priceScore * 0.25;
                factors["price"] = priceScore;

                // 3. Сходство описания (20%)
                if (!string.IsNullOrEmpty(otherComputer.Description))
                {
                    var otherDesc = otherComputer.Description.ToLower();
                    var currentWords = currentDescription.Split(' ', ',', '.', ';', ':', '-')
                        .Where(w => w.Length > 2)
                        .ToList();
                    var otherWords = otherDesc.Split(' ', ',', '.', ';', ':', '-')
                        .Where(w => w.Length > 2)
                        .ToList();

                    var commonWords = currentWords.Intersect(otherWords).Count();
                    var wordScore = currentWords.Count > 0 ? (double)commonWords / currentWords.Count : 0;
                    score += wordScore * 0.2;
                    factors["description"] = wordScore;
                }

                // 4. Популярность (15%)
                var hasComponents = otherComputer.ComputerComponents.Any();
                var componentScoreBonus = hasComponents ? 0.15 : 0;
                score += componentScoreBonus;
                factors["popularity"] = componentScoreBonus;

                // 5. Наличие (10%)
                var stockScore = otherComputer.Quantity > 5 ? 0.1 :
                                otherComputer.Quantity > 0 ? 0.05 : 0;
                score += stockScore;
                factors["stock"] = stockScore;

                scoredComputers.Add((otherComputer, score, factors));
            }

            return scoredComputers
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new RecommendationItem
                {
                    Id = x.Computer.Id,
                    ProductType = "Computer",
                    Name = x.Computer.Name,
                    Price = x.Computer.Price,
                    ImageUrl = x.Computer.ImageUrl,
                    Description = x.Computer.Description,
                    StockQuantity = x.Computer.Quantity,
                    RelevanceScore = x.Score,
                    RecommendationType = "similar",
                    ScoreFactors = x.Factors // Используем x.Factors из кортежа
                })
                .ToList();
        }

        public async Task<List<RecommendationItem>> GetCartRecommendationsAsync(Cart cart, int? userId, string? guestId)
        {
            var recommendations = new List<RecommendationItem>();

            // Анализируем содержимое корзины
            var computerIds = cart.Items.Where(i => i.IsComputer).Select(i => i.ComputerId).ToList();
            var componentIds = cart.Items.Where(i => i.IsComponent).Select(i => i.ComponentId).ToList();

            // 1. Дополняющие товары
            if (computerIds.Any())
            {
                // Для компьютеров предлагаем периферию или апгрейд
                var complementary = await GetComplementaryForComputersAsync(computerIds, 3);
                recommendations.AddRange(complementary);
            }

            if (componentIds.Any())
            {
                // Для компонентов предлагаем совместимые компоненты
                var components = await _context.Components
                    .Where(c => componentIds.Contains(c.Id))
                    .ToListAsync();

                var compatible = await FindCompatibleComponentsAsync(components, 3);
                recommendations.AddRange(compatible);
            }

            // 2. Часто покупаемые вместе (на основе статистики)
            var frequentlyBought = await GetFrequentlyBoughtTogetherAsync(computerIds, componentIds, 2);
            recommendations.AddRange(frequentlyBought);

            // Убираем то, что уже в корзине
            recommendations = recommendations
                .Where(r => !cart.Items.Any(i =>
                    (i.IsComputer && i.ComputerId == r.Id && r.ProductType == "Computer") ||
                    (i.IsComponent && i.ComponentId == r.Id && r.ProductType == "Component")))
                .ToList();

            return recommendations
                .OrderByDescending(r => r.RelevanceScore)
                .Take(6)
                .ToList();
        }

        public async Task<List<RecommendationItem>> GetPopularItemsAsync(string? productType = null, int limit = 10)
        {
            var cacheKey = string.Format(POPULAR_CACHE_KEY, productType ?? "all", limit);

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _recommendationTTL["popular"];

                var popularItems = new List<RecommendationItem>();

                if (productType == null || productType == "Computer")
                {
                    // Популярные компьютеры (по продажам/просмотрам)
                    var popularComputers = await GetPopularComputersAsync(limit / 2);
                    popularItems.AddRange(popularComputers);
                }

                if (productType == null || productType == "Component")
                {
                    // Популярные компоненты
                    var popularComponents = await GetPopularComponentsAsync(limit / 2);
                    popularItems.AddRange(popularComponents);
                }

                return popularItems
                    .OrderByDescending(r => r.RelevanceScore)
                    .Take(limit)
                    .ToList();
            }) ?? new List<RecommendationItem>();
        }

        private async Task<List<RecommendationItem>> GetPopularComputersAsync(int limit)
        {
            // В реальности здесь была бы аналитика продаж/просмотров
            // Пока используем простую логику: новые и в наличии
            var computers = await _context.Computers
                .Where(c => c.Quantity > 0)
                .OrderByDescending(c => c.Id) // Новые первыми
                .ThenByDescending(c => c.Quantity) // В наличии
                .Take(limit * 2) // Берем больше для фильтрации
                .ToListAsync();

            var random = new Random();
            return computers
                .OrderBy(x => random.Next()) // Немного рандомизируем
                .Take(limit)
                .Select(c => new RecommendationItem
                {
                    Id = c.Id,
                    ProductType = "Computer",
                    Name = c.Name,
                    Price = c.Price,
                    ImageUrl = c.ImageUrl,
                    Description = c.Description,
                    StockQuantity = c.Quantity,
                    RelevanceScore = 0.7 + (random.NextDouble() * 0.3), // 0.7-1.0
                    RecommendationType = "popular"
                })
                .ToList();
        }

        private async Task<List<RecommendationItem>> GetPopularComponentsAsync(int limit)
        {
            var components = await _context.Components
                .Where(c => c.Quantity > 0)
                .OrderByDescending(c => c.Id)
                .ThenByDescending(c => c.Quantity)
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
                    RelevanceScore = 0.6 + (random.NextDouble() * 0.4), // 0.6-1.0
                    RecommendationType = "popular"
                })
                .ToList();
        }

        private double CalculatePriceSimilarity(decimal price1, decimal price2)
        {
            if (price1 == 0 && price2 == 0) return 1.0;

            var d1 = (double)price1;
            var d2 = (double)price2;
            var diff = Math.Abs(d1 - d2);
            var max = Math.Max(d1, d2);

            return max > 0 ? 1.0 - (diff / max) : 0;
        }

        public async Task<List<RecommendationItem>> GetGuestRecommendationsAsync(string guestId, int limit = 6)
        {
            var cacheKey = string.Format(GUEST_CACHE_KEY, guestId);

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _recommendationTTL["personalized"];

                // Получаем историю действий гостя
                if (_guestActions.TryGetValue($"guest_{guestId}", out var actions) && actions.Any())
                {
                    // Анализируем историю
                    var viewedProducts = actions
                        .Where(a => a.ActionType == UserActionType.View ||
                                   a.ActionType == UserActionType.AddToCart)
                        .GroupBy(a => new { a.ProductType, a.ProductId })
                        .Select(g => new
                        {
                            g.Key.ProductType,
                            g.Key.ProductId,
                            TotalWeight = g.Sum(a => a.Weight),
                            LastSeen = g.Max(a => a.Timestamp)
                        })
                        .OrderByDescending(x => x.TotalWeight)
                        .ThenByDescending(x => x.LastSeen)
                        .Take(5)
                        .ToList();

                    var recommendations = new List<RecommendationItem>();

                    foreach (var product in viewedProducts)
                    {
                        var similar = await GetSimilarProductsAsync(
                            product.ProductId,
                            product.ProductType,
                            2);
                        recommendations.AddRange(similar);
                    }

                    // Добавляем популярные, если мало рекомендаций
                    if (recommendations.Count < limit)
                    {
                        var popular = await GetPopularItemsAsync(null, limit - recommendations.Count);
                        recommendations.AddRange(popular);
                    }

                    return recommendations
                        .GroupBy(r => new { r.ProductType, r.Id })
                        .Select(g => g.First())
                        .Take(limit)
                        .ToList();
                }

                // Если нет истории - возвращаем популярные
                return await GetPopularItemsAsync(null, limit);
            }) ?? new List<RecommendationItem>();
        }

        public async Task<List<RecommendationItem>> GetPersonalRecommendationsAsync(int userId, int limit = 10)
        {
            var cacheKey = string.Format(PERSONAL_CACHE_KEY, userId);

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _recommendationTTL["personalized"];

                // Получаем историю действий пользователя
                if (_userActions.TryGetValue($"user_{userId}", out var actions) && actions.Any())
                {
                    // Анализируем предпочтения
                    var productTypes = actions
                        .GroupBy(a => a.ProductType)
                        .Select(g => new { Type = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .FirstOrDefault();

                    var favoriteCategories = actions
                        .Where(a => a.Category != null)
                        .GroupBy(a => a.Category)
                        .Select(g => new { Category = g.Key!, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(3)
                        .ToList();

                    var recommendations = new List<RecommendationItem>();

                    // 1. На основе любимых категорий
                    foreach (var category in favoriteCategories)
                    {
                        var categoryItems = await GetItemsByCategoryAsync(category.Category, 2);
                        recommendations.AddRange(categoryItems);
                    }

                    // 2. На основе последних просмотров
                    var recentViews = actions
                        .Where(a => a.ActionType == UserActionType.View)
                        .OrderByDescending(a => a.Timestamp)
                        .Take(3)
                        .ToList();

                    foreach (var view in recentViews)
                    {
                        var similar = await GetSimilarProductsAsync(
                            view.ProductId,
                            view.ProductType,
                            1);
                        recommendations.AddRange(similar);
                    }

                    // 3. Добавляем популярные в его категориях
                    if (recommendations.Count < limit)
                    {
                        var needed = limit - recommendations.Count;
                        var popularInCategory = await GetPopularInCategoriesAsync(
                            favoriteCategories.Select(fc => fc.Category!).ToList(),
                            needed);
                        recommendations.AddRange(popularInCategory);
                    }

                    return recommendations
                        .GroupBy(r => new { r.ProductType, r.Id })
                        .Select(g => g.First())
                        .Take(limit)
                        .ToList();
                }

                // Если нет истории - возвращаем популярные
                return await GetPopularItemsAsync(null, limit);
            }) ?? new List<RecommendationItem>();
        }

        private async Task<List<RecommendationItem>> GetItemsByCategoryAsync(string category, int limit)
        {
            if (category == "Computer")
            {
                return await GetPopularComputersAsync(limit);
            }

            var components = await _context.Components
                .Where(c => c.Type == category && c.Quantity > 0)
                .OrderByDescending(c => c.Id)
                .Take(limit)
                .ToListAsync();

            var random = new Random();
            return components
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
                    RelevanceScore = 0.8 + (random.NextDouble() * 0.2),
                    RecommendationType = "personalized"
                })
                .ToList();
        }

        private async Task<List<RecommendationItem>> GetPopularInCategoriesAsync(List<string> categories, int limit)
        {
            var items = new List<RecommendationItem>();

            foreach (var category in categories)
            {
                if (items.Count >= limit) break;

                var categoryItems = await GetItemsByCategoryAsync(category, limit / categories.Count);
                items.AddRange(categoryItems);
            }

            return items.Take(limit).ToList();
        }

        private async Task<List<RecommendationItem>> GetComplementaryForComputersAsync(List<int> computerIds, int limit)
        {
            // Для компьютеров предлагаем:
            // 1. Мониторы
            // 2. Клавиатуры/мыши
            // 3. Апгрейд компонентов

            var complementary = new List<RecommendationItem>();

            // Мониторы (предполагаем, что есть тип "Monitor")
            var monitors = await _context.Components
                .Where(c => c.Type == "Monitor" && c.Quantity > 0)
                .Take(limit)
                .ToListAsync();

            var random = new Random();
            complementary.AddRange(monitors.Select(m => new RecommendationItem
            {
                Id = m.Id,
                ProductType = "Component",
                ComponentType = m.Type,
                Name = m.Name,
                Price = m.Price,
                ImageUrl = m.ImageUrl,
                Description = m.Description,
                StockQuantity = m.Quantity,
                RelevanceScore = 0.8 + (random.NextDouble() * 0.2),
                RecommendationType = "complementary"
            }));

            return complementary.Take(limit).ToList();
        }

        private async Task<List<RecommendationItem>> FindCompatibleComponentsAsync(List<Component> selectedComponents, int limit)
        {
            var compatible = new List<RecommendationItem>();

            // Находим материнскую плату
            var motherboard = selectedComponents.FirstOrDefault(c => c.Type == "MB");
            if (motherboard != null)
            {
                // Ищем совместимые компоненты
                var cpuSocket = motherboard.Socket;
                var memoryType = motherboard.MemoryType;

                // Совместимые процессоры
                var compatibleCPUs = await _context.Components
                    .Where(c => c.Type == "CPU" && c.Socket == cpuSocket && c.Quantity > 0)
                    .Take(2)
                    .ToListAsync();

                // Совместимая память
                var compatibleRAM = await _context.Components
                    .Where(c => c.Type == "RAM" && c.MemoryType == memoryType && c.Quantity > 0)
                    .Take(2)
                    .ToListAsync();

                var random = new Random();
                compatible.AddRange(compatibleCPUs.Select(c => new RecommendationItem
                {
                    Id = c.Id,
                    ProductType = "Component",
                    ComponentType = c.Type,
                    Name = c.Name,
                    Price = c.Price,
                    ImageUrl = c.ImageUrl,
                    Description = c.Description,
                    StockQuantity = c.Quantity,
                    RelevanceScore = 0.9 + (random.NextDouble() * 0.1),
                    RecommendationType = "compatible"
                }));

                compatible.AddRange(compatibleRAM.Select(c => new RecommendationItem
                {
                    Id = c.Id,
                    ProductType = "Component",
                    ComponentType = c.Type,
                    Name = c.Name,
                    Price = c.Price,
                    ImageUrl = c.ImageUrl,
                    Description = c.Description,
                    StockQuantity = c.Quantity,
                    RelevanceScore = 0.85 + (random.NextDouble() * 0.15),
                    RecommendationType = "compatible"
                }));
            }

            return compatible.Take(limit).ToList();
        }

        private async Task<List<RecommendationItem>> GetFrequentlyBoughtTogetherAsync(
            List<int> computerIds, List<int> componentIds, int limit)
        {
            // В реальном приложении здесь был бы анализ истории заказов
            // Пока возвращаем случайные популярные товары

            return await GetPopularItemsAsync(null, limit);
        }

        // Вспомогательные методы
        private int GetCommonTerms(string text1, string text2)
        {
            var terms1 = text1.Split(' ', ',', '.', ';', ':', '-')
                .Where(t => t.Length > 2)
                .Select(t => t.ToLower())
                .Distinct()
                .ToList();

            var terms2 = text2.Split(' ', ',', '.', ';', ':', '-')
                .Where(t => t.Length > 2)
                .Select(t => t.ToLower())
                .Distinct()
                .ToList();

            return terms1.Intersect(terms2).Count();
        }

        private int GetTermCount(string text)
        {
            return text.Split(' ', ',', '.', ';', ':', '-')
                .Count(t => t.Length > 2);
        }

        private bool AreSameBrand(string name1, string name2)
        {
            var brands = new[] { "Intel", "AMD", "NVIDIA", "ASUS", "MSI", "Gigabyte", "Kingston", "Corsair", "Samsung" };

            var brand1 = brands.FirstOrDefault(b => name1.Contains(b, StringComparison.OrdinalIgnoreCase));
            var brand2 = brands.FirstOrDefault(b => name2.Contains(b, StringComparison.OrdinalIgnoreCase));

            return brand1 != null && brand2 != null && brand1 == brand2;
        }

        public async Task CleanupOldDataAsync()
        {
            try
            {
                var cutoff = DateTime.UtcNow - _actionLifetime;

                // Очищаем старые действия пользователей
                foreach (var key in _userActions.Keys.ToList())
                {
                    if (_userActions.TryGetValue(key, out var actions))
                    {
                        var filtered = actions.Where(a => a.Timestamp >= cutoff).ToList();
                        if (filtered.Count == 0)
                        {
                            _userActions.TryRemove(key, out _);
                        }
                        else
                        {
                            _userActions[key] = filtered;
                        }
                    }
                }

                // Очищаем старые действия гостей
                foreach (var key in _guestActions.Keys.ToList())
                {
                    if (_guestActions.TryGetValue(key, out var actions))
                    {
                        var filtered = actions.Where(a => a.Timestamp >= cutoff).ToList();
                        if (filtered.Count == 0)
                        {
                            _guestActions.TryRemove(key, out _);
                        }
                        else
                        {
                            _guestActions[key] = filtered;
                        }
                    }
                }

                _logger.LogInformation("Cleaned up old recommendation data. User actions: {UserCount}, Guest actions: {GuestCount}",
                    _userActions.Count, _guestActions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old recommendation data");
            }
        }

        private async Task CleanupOldDataPeriodically()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromHours(6)); // Очищаем каждые 6 часов
                await CleanupOldDataAsync();
            }
        }

        public void Dispose()
        {
            // Очистка ресурсов
            _userActions.Clear();
            _guestActions.Clear();
            _productAssociations.Clear();
        }
    }
}