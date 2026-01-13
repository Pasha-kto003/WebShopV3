using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using WebShopV3.Json.Models;
using WebShopV3.Json.Models.Recommendation;

namespace WebShopV3.Json.Services
{
    public interface IRecommendationService
    {
        Task TrackActionAsync(UserAction action);
        Task<List<RecommendationItem>> GetRecommendationsAsync(RecommendationRequest request);
        Task<List<RecommendationItem>> GetSimilarProductsAsync(int productId, string productType, int limit = 5);
        Task<List<RecommendationItem>> GetCartRecommendationsAsync(Cart cart, int? userId, string? guestId);
        Task<List<RecommendationItem>> GetPopularItemsAsync(string? productType = null, int limit = 10);
        Task<List<RecommendationItem>> GetGuestRecommendationsAsync(string guestId, int limit = 6);
        Task<List<RecommendationItem>> GetPersonalRecommendationsAsync(int userId, int limit = 10);
        Task CleanupOldDataAsync();
    }

    public class RecommendationService : IRecommendationService
    {
        private readonly string _dataPath;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RecommendationService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly CompatibilityService _compatibilityService;

        private readonly ConcurrentDictionary<string, List<UserAction>> _userActions = new();
        private readonly ConcurrentDictionary<string, List<UserAction>> _guestActions = new();

        private const int MAX_ACTIONS_PER_USER = 500;
        private const int RECOMMENDATION_CACHE_MINUTES = 10;
        private const int ACTION_CLEANUP_HOURS = 24;
        private const string CACHE_PREFIX = "rec_";
        private const string ACTIONS_FILE = "user_actions.json";

        public RecommendationService(
            string dataPath,
            IMemoryCache cache,
            ILogger<RecommendationService> logger,
            IHttpContextAccessor httpContextAccessor,
            IHostApplicationLifetime appLifetime,
            CompatibilityService compatibilityService)
        {
            _dataPath = dataPath;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _appLifetime = appLifetime;
            _compatibilityService = compatibilityService;

            StartPeriodicCleanup();
        }

        public async Task TrackActionAsync(UserAction action)
        {
            if (action == null) return;

            try
            {
                var cacheKey = GetActionStorageKey(action);
                var storage = action.UserId.HasValue ? _userActions : _guestActions;

                await Task.Run(() =>
                {
                    storage.AddOrUpdate(cacheKey,
                        new List<UserAction> { action },
                        (key, existingActions) =>
                        {
                            existingActions.Add(action);

                            if (existingActions.Count > MAX_ACTIONS_PER_USER)
                            {
                                existingActions = existingActions
                                    .Where(a => (DateTime.UtcNow - a.Timestamp).TotalHours < ACTION_CLEANUP_HOURS)
                                    .OrderByDescending(a => a.Timestamp)
                                    .Take(MAX_ACTIONS_PER_USER / 2)
                                    .ToList();
                            }

                            return existingActions;
                        });
                });

                // Сохраняем действие в JSON файл
                await SaveActionToJsonAsync(action);

                await InvalidateUserCacheAsync(action.UserId, action.GuestId);

                _logger.LogDebug("Action tracked: {UserId}/{GuestId} - {ActionType} - {ProductId}",
                    action.UserId, action.GuestId, action.ActionType, action.ProductId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking action");
            }
        }

        private async Task SaveActionToJsonAsync(UserAction action)
        {
            try
            {
                var filePath = Path.Combine(_dataPath, ACTIONS_FILE);
                var directory = Path.GetDirectoryName(filePath);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory!);

                List<UserAction> actions = new();

                if (File.Exists(filePath))
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    actions = JsonSerializer.Deserialize<List<UserAction>>(json) ?? new List<UserAction>();
                }

                action.Id = GetNextActionId(actions);
                action.Timestamp = DateTime.UtcNow;
                actions.Add(action);

                // Сохраняем только последние 1000 действий
                if (actions.Count > 1000)
                {
                    actions = actions.OrderByDescending(a => a.Timestamp).Take(1000).ToList();
                }

                var serialized = JsonSerializer.Serialize(actions, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(filePath, serialized);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving action to JSON");
            }
        }

        private int GetNextActionId(List<UserAction> actions)
        {
            return actions.Any() ? actions.Max(a => a.Id) + 1 : 1;
        }

        public async Task<List<RecommendationItem>> GetRecommendationsAsync(RecommendationRequest request)
        {
            if (request == null) return new List<RecommendationItem>();

            var cacheKey = $"{CACHE_PREFIX}rec_{request.UserId}_{request.GuestId}_{request.CurrentProductId}_{request.Limit}_{request.IncludeComputers}_{request.IncludeComponents}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(RECOMMENDATION_CACHE_MINUTES);
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);

                var recommendations = new List<RecommendationItem>();

                // 1. Рекомендации на основе текущего товара
                if (request.CurrentProductId.HasValue && !string.IsNullOrEmpty(request.CurrentProductType))
                {
                    var similar = await GetSimilarProductsAsync(
                        request.CurrentProductId.Value,
                        request.CurrentProductType,
                        (int)(request.Limit * 0.3));
                    recommendations.AddRange(similar);
                }

                // 2. Персональные рекомендации
                if (request.UserId.HasValue)
                {
                    var personal = await GetPersonalRecommendationsAsync(
                        request.UserId.Value,
                        (int)(request.Limit * 0.4));
                    recommendations.AddRange(personal);
                }
                else if (!string.IsNullOrEmpty(request.GuestId))
                {
                    var guest = await GetGuestRecommendationsAsync(
                        request.GuestId,
                        (int)(request.Limit * 0.4));
                    recommendations.AddRange(guest);
                }

                // 3. Популярные товары
                var needed = request.Limit - recommendations.Count;
                if (needed > 0)
                {
                    var popular = await GetPopularItemsAsync(null, needed);
                    recommendations.AddRange(popular);
                }

                // Фильтруем по типам
                recommendations = recommendations
                    .Where(r => (r.ProductType == "Computer" && request.IncludeComputers) ||
                               (r.ProductType == "Component" && request.IncludeComponents))
                    .ToList();

                // Удаляем дубликаты
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
            var cacheKey = $"{CACHE_PREFIX}similar_{productType}_{productId}_{limit}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);

                if (productType == "Computer")
                {
                    return await FindSimilarComputersAsync(productId, limit);
                }
                else if (productType == "Component")
                {
                    return await FindSimilarComponentsAsync(productId, limit);
                }

                return new List<RecommendationItem>();
            }) ?? new List<RecommendationItem>();
        }

        private async Task<List<RecommendationItem>> FindSimilarComputersAsync(int computerId, int limit)
        {
            // Загружаем компьютеры из JSON
            var computers = await LoadComputersAsync();
            var computer = computers.FirstOrDefault(c => c.Id == computerId);

            if (computer == null) return new List<RecommendationItem>();

            var currentPrice = computer.Price;
            var random = new Random();

            // Получаем категорию компьютера из названия или описания
            var computerCategory = GetComputerCategory(computer);

            // Ищем похожие компьютеры по категории и цене
            var similarComputers = computers
                .Where(c => c.Id != computerId && c.Quantity > 0)
                .Select(c => new
                {
                    Computer = c,
                    CategoryMatch = GetComputerCategory(c) == computerCategory ? 1 : 0,
                    PriceDifference = Math.Abs(c.Price - currentPrice),
                    NameSimilarity = CalculateNameSimilarity(computer.Name, c.Name)
                })
                .OrderByDescending(x => x.CategoryMatch)
                .ThenByDescending(x => x.NameSimilarity)
                .ThenBy(x => x.PriceDifference)
                .Take(limit * 2)
                .ToList();

            var recommendations = new List<RecommendationItem>();

            foreach (var item in similarComputers)
            {
                var categoryScore = item.CategoryMatch * 0.4;
                var nameScore = item.NameSimilarity * 0.3;
                var priceScore = (double)(1 - Math.Min(item.PriceDifference / currentPrice, 1)) * 0.3;
                var totalScore = categoryScore + nameScore + priceScore;

                recommendations.Add(new RecommendationItem
                {
                    Id = item.Computer.Id,
                    ProductType = "Computer",
                    Name = item.Computer.Name,
                    Price = item.Computer.Price,
                    ImageUrl = item.Computer.ImageUrl,
                    Description = item.Computer.Description,
                    StockQuantity = item.Computer.Quantity,
                    RelevanceScore = totalScore,
                    RecommendationType = "similar",
                    ScoreFactors = new Dictionary<string, double>
                    {
                        { "category_match", item.CategoryMatch },
                        { "name_similarity", item.NameSimilarity },
                        { "price_similarity", (double)(1 - Math.Min(item.PriceDifference / currentPrice, 1)) }
                    }
                });
            }

            return recommendations
                .OrderByDescending(r => r.RelevanceScore)
                .Take(limit)
                .ToList();
        }

        // Вспомогательные методы для анализа компьютеров
        private string GetComputerCategory(Computer computer)
        {
            var name = computer.Name.ToLower();

            if (name.Contains("игровой") || name.Contains("gaming") || name.Contains("игры"))
                return "Gaming";
            if (name.Contains("рабочий") || name.Contains("работа") || name.Contains("workstation"))
                return "Workstation";
            if (name.Contains("офисный") || name.Contains("office") || name.Contains("офис"))
                return "Office";
            if (name.Contains("домашний") || name.Contains("home") || name.Contains("семейный"))
                return "Home";
            if (name.Contains("бюджетный") || name.Contains("дешевый") || name.Contains("economy"))
                return "Budget";
            if (name.Contains("мощный") || name.Contains("high-end") || name.Contains("профессиональный"))
                return "HighEnd";

            return "General";
        }

        private double CalculateNameSimilarity(string name1, string name2)
        {
            if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2))
                return 0;

            name1 = name1.ToLower();
            name2 = name2.ToLower();

            // Простая логика сравнения по ключевым словам
            var keywords1 = name1.Split(' ', '-', ',', '.', ';').Where(k => k.Length > 2).ToArray();
            var keywords2 = name2.Split(' ', '-', ',', '.', ';').Where(k => k.Length > 2).ToArray();

            if (!keywords1.Any() || !keywords2.Any())
                return 0;

            var commonKeywords = keywords1.Intersect(keywords2).Count();
            var totalKeywords = Math.Max(keywords1.Length, keywords2.Length);

            return (double)commonKeywords / totalKeywords;
        }

        private async Task<List<RecommendationItem>> FindSimilarComponentsAsync(int componentId, int limit)
        {
            // Загружаем компоненты из JSON
            var components = await LoadComponentsAsync();
            var component = components.FirstOrDefault(c => c.Id == componentId);

            if (component == null) return new List<RecommendationItem>();

            var componentType = component.Type ?? "";
            var currentPrice = component.Price;
            var random = new Random();

            var similarComponents = components
                .Where(c => c.Id != componentId &&
                           c.Type == componentType &&
                           c.Quantity > 0)
                .Select(c => new
                {
                    Component = c,
                    PriceDifference = Math.Abs(c.Price - currentPrice)
                })
                .OrderBy(x => x.PriceDifference)
                .ThenByDescending(c => c.Component.Quantity)
                .Take(limit * 2)
                .ToList();

            var recommendations = new List<RecommendationItem>();

            foreach (var item in similarComponents)
            {
                var priceSimilarity = (double)(1 - Math.Min(item.PriceDifference / currentPrice, 1));
                var score = 0.6 + (priceSimilarity * 0.4);

                recommendations.Add(new RecommendationItem
                {
                    Id = item.Component.Id,
                    ProductType = "Component",
                    ComponentType = item.Component.Type,
                    Name = item.Component.Name,
                    Price = item.Component.Price,
                    ImageUrl = item.Component.ImageUrl,
                    Description = item.Component.Description,
                    StockQuantity = item.Component.Quantity,
                    RelevanceScore = score,
                    RecommendationType = "similar",
                    ScoreFactors = new Dictionary<string, double>
                    {
                        { "price_similarity", priceSimilarity },
                        { "availability", item.Component.Quantity > 10 ? 0.2 : 0.1 }
                    }
                });
            }

            return recommendations
                .OrderByDescending(r => r.RelevanceScore)
                .Take(limit)
                .ToList();
        }

        public async Task<List<RecommendationItem>> GetCartRecommendationsAsync(Cart cart, int? userId, string? guestId)
        {
            if (cart == null || !cart.Items.Any())
                return await GetPopularItemsAsync(null, 6);

            var cacheKey = $"{CACHE_PREFIX}cart_{userId}_{guestId}_{cart.TotalItems}_{cart.TotalAmount}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                var recommendations = new List<RecommendationItem>();

                // Анализируем корзину
                var computerIds = cart.Items.Where(i => i.IsComputer).Select(i => i.ComputerId).ToList();
                var componentIds = cart.Items.Where(i => i.IsComponent).Select(i => i.ComponentId).ToList();

                if (computerIds.Any())
                {
                    var firstComputerId = computerIds.First();
                    var computers = await LoadComputersAsync();
                    var computer = computers.FirstOrDefault(c => c.Id == firstComputerId);

                    if (computer != null)
                    {
                        // Получаем тип компьютера из названия или категории
                        var computerType = GetComputerCategory(computer);
                        var components = await LoadComponentsAsync();

                        // Рекомендуем компоненты, подходящие к типу компьютера
                        var recommendedComponentTypes = GetRecommendedComponentsForComputerType(computerType);

                        var additionalComponents = components
                            .Where(c => recommendedComponentTypes.Contains(c.Type) &&
                                       c.Quantity > 0 &&
                                       !componentIds.Contains(c.Id))
                            .OrderBy(c => Guid.NewGuid())
                            .Take(3)
                            .ToList();

                        foreach (var comp in additionalComponents)
                        {
                            recommendations.Add(new RecommendationItem
                            {
                                Id = comp.Id,
                                ProductType = "Component",
                                ComponentType = comp.Type,
                                Name = comp.Name,
                                Price = comp.Price,
                                ImageUrl = comp.ImageUrl,
                                Description = comp.Description,
                                StockQuantity = comp.Quantity,
                                RelevanceScore = 0.8,
                                RecommendationType = "complementary"
                            });
                        }
                    }
                }

                // Рекомендуем дополнительные аксессуары и периферию
                if (componentIds.Any())
                {
                    var components = await LoadComponentsAsync();
                    var cartComponents = components
                        .Where(c => componentIds.Contains(c.Id))
                        .ToList();

                    // Определяем, какие типы компонентов уже есть в корзине
                    var existingTypes = cartComponents
                        .Select(c => c.Type)
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct()
                        .ToList();

                    // Рекомендуем совместимые компоненты
                    var recommendedForTypes = new List<string>();
                    foreach (var type in existingTypes)
                    {
                        recommendedForTypes.AddRange(GetCompatibleComponentTypes(type));
                    }

                    if (recommendedForTypes.Any())
                    {
                        var compatibleComponents = components
                            .Where(c => recommendedForTypes.Contains(c.Type) &&
                                       c.Quantity > 0 &&
                                       !componentIds.Contains(c.Id))
                            .OrderBy(c => Guid.NewGuid())
                            .Take(2)
                            .ToList();

                        foreach (var comp in compatibleComponents)
                        {
                            recommendations.Add(new RecommendationItem
                            {
                                Id = comp.Id,
                                ProductType = "Component",
                                ComponentType = comp.Type,
                                Name = comp.Name,
                                Price = comp.Price,
                                ImageUrl = comp.ImageUrl,
                                Description = comp.Description,
                                StockQuantity = comp.Quantity,
                                RelevanceScore = 0.7,
                                RecommendationType = "compatible"
                            });
                        }
                    }
                }

                // Рекомендуем похожие компьютеры (если в корзине уже есть компьютер)
                if (computerIds.Any() && recommendations.Count < 6)
                {
                    var firstComputerId = computerIds.First();
                    var similarComputers = await FindSimilarComputersAsync(firstComputerId, 2);

                    // Фильтруем те, что уже в корзине
                    var similarNotInCart = similarComputers
                        .Where(c => !computerIds.Contains(c.Id))
                        .Take(2);

                    recommendations.AddRange(similarNotInCart);
                }

                // Добавляем популярные товары, если нужно
                if (recommendations.Count < 6)
                {
                    var needed = 6 - recommendations.Count;
                    var popular = await GetPopularItemsAsync(null, needed);
                    recommendations.AddRange(popular);
                }

                // Убираем дубликаты и товары уже в корзине
                return recommendations
                    .Where(r => !cart.Items.Any(i =>
                        (i.IsComputer && i.ComputerId == r.Id && r.ProductType == "Computer") ||
                        (i.IsComponent && i.ComponentId == r.Id && r.ProductType == "Component")))
                    .Take(6)
                    .ToList();
            }) ?? new List<RecommendationItem>();
        }

        private List<string> GetRecommendedComponentsForComputerType(string computerType)
        {
            return computerType switch
            {
                "Gaming" => new List<string> { "GPU", "RAM", "SSD", "Cooler", "Monitor", "Keyboard", "Mouse" },
                "Workstation" => new List<string> { "CPU", "RAM", "SSD", "GPU", "Monitor", "UPS" },
                "Office" => new List<string> { "Monitor", "Keyboard", "Mouse", "Webcam", "UPS" },
                "Home" => new List<string> { "Monitor", "Webcam", "Speakers", "WiFi" },
                "Budget" => new List<string> { "HDD", "RAM", "Mouse", "Keyboard" },
                "HighEnd" => new List<string> { "GPU", "CPU", "RAM", "SSD", "Cooler", "Monitor", "UPS" },
                "Premium" => new List<string> { "GPU", "CPU", "RAM", "SSD", "Cooler", "Monitor", "UPS" },
                _ => new List<string> { "Monitor", "Keyboard", "Mouse", "UPS" }
            };
        }

        private List<string> GetCompatibleComponentTypes(string componentType)
        {
            // Определяем, какие типы компонентов совместимы с данным типом
            var compatibilityMatrix = new Dictionary<string, List<string>>
            {
                { "CPU", new List<string> { "MB", "Cooler", "RAM" } },
                { "MB", new List<string> { "CPU", "RAM", "GPU", "SSD", "HDD", "PSU", "Case" } },
                { "GPU", new List<string> { "PSU", "MB", "Monitor" } },
                { "RAM", new List<string> { "MB", "CPU" } },
                { "SSD", new List<string> { "MB", "Case", "HDD" } },
                { "HDD", new List<string> { "MB", "Case", "SSD" } },
                { "PSU", new List<string> { "MB", "GPU", "CPU", "Case" } },
                { "Case", new List<string> { "MB", "PSU", "Cooler", "GPU" } },
                { "Cooler", new List<string> { "CPU", "Case", "MB" } },
                { "Monitor", new List<string> { "GPU", "CPU", "MB" } }
            };

            return compatibilityMatrix.TryGetValue(componentType, out var compatibleTypes)
                ? compatibleTypes
                : new List<string>();
        }

        public async Task<List<RecommendationItem>> GetPopularItemsAsync(string? productType = null, int limit = 10)
        {
            var cacheKey = $"{CACHE_PREFIX}popular_{productType ?? "all"}_{limit}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var popularItems = new List<RecommendationItem>();
                var random = new Random();

                // Компьютеры
                if (productType == null || productType == "Computer")
                {
                    var computers = await LoadComputersAsync();
                    var popularComputers = computers
                        .Where(c => c.Quantity > 0)
                        .OrderByDescending(c => c.Id) // Сначала новые
                        .Take(limit)
                        .ToList();

                    foreach (var computer in popularComputers)
                    {
                        popularItems.Add(new RecommendationItem
                        {
                            Id = computer.Id,
                            ProductType = "Computer",
                            Name = computer.Name,
                            Price = computer.Price,
                            ImageUrl = computer.ImageUrl,
                            Description = computer.Description,
                            StockQuantity = computer.Quantity,
                            RelevanceScore = 0.6 + (random.NextDouble() * 0.3),
                            RecommendationType = "popular"
                        });
                    }
                }

                // Комплектующие
                if (productType == null || productType == "Component")
                {
                    var components = await LoadComponentsAsync();
                    var popularComponents = components
                        .Where(c => c.Quantity > 0)
                        .OrderByDescending(c => c.Id) // Сначала новые
                        .Take(limit)
                        .ToList();

                    foreach (var component in popularComponents)
                    {
                        popularItems.Add(new RecommendationItem
                        {
                            Id = component.Id,
                            ProductType = "Component",
                            ComponentType = component.Type,
                            Name = component.Name,
                            Price = component.Price,
                            ImageUrl = component.ImageUrl,
                            Description = component.Description,
                            StockQuantity = component.Quantity,
                            RelevanceScore = 0.5 + (random.NextDouble() * 0.3),
                            RecommendationType = "popular"
                        });
                    }
                }

                return popularItems
                    .OrderByDescending(r => r.RelevanceScore)
                    .Take(limit)
                    .ToList();
            }) ?? new List<RecommendationItem>();
        }

        public async Task<List<RecommendationItem>> GetGuestRecommendationsAsync(string guestId, int limit = 6)
        {
            var cacheKey = $"{CACHE_PREFIX}guest_{guestId}_{limit}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20);

                // Пытаемся получить историю из памяти
                if (_guestActions.TryGetValue($"guest_{guestId}", out var actions) && actions.Any())
                {
                    var recentActions = actions
                        .Where(a => a.Timestamp > DateTime.UtcNow.AddHours(-24))
                        .GroupBy(a => new { a.ProductType, a.ProductId })
                        .Select(g => new
                        {
                            g.Key.ProductType,
                            g.Key.ProductId,
                            Weight = g.Sum(a => a.Weight)
                        })
                        .OrderByDescending(x => x.Weight)
                        .Take(3)
                        .ToList();

                    var recommendations = new List<RecommendationItem>();

                    foreach (var action in recentActions)
                    {
                        var similar = await GetSimilarProductsAsync(
                            action.ProductId,
                            action.ProductType,
                            2);
                        recommendations.AddRange(similar);
                    }

                    if (recommendations.Count < limit)
                    {
                        var needed = limit - recommendations.Count;
                        var popular = await GetPopularItemsAsync(null, needed);
                        recommendations.AddRange(popular);
                    }

                    return recommendations.Take(limit).ToList();
                }

                // Если нет истории в памяти, загружаем из JSON
                var jsonActions = await LoadActionsFromJsonAsync();
                var guestActions = jsonActions
                    .Where(a => a.GuestId == guestId && a.Timestamp > DateTime.UtcNow.AddHours(-24))
                    .ToList();

                if (guestActions.Any())
                {
                    var recentActions = guestActions
                        .GroupBy(a => new { a.ProductType, a.ProductId })
                        .Select(g => new
                        {
                            g.Key.ProductType,
                            g.Key.ProductId,
                            Weight = g.Sum(a => a.Weight)
                        })
                        .OrderByDescending(x => x.Weight)
                        .Take(3)
                        .ToList();

                    var recommendations = new List<RecommendationItem>();

                    foreach (var action in recentActions)
                    {
                        var similar = await GetSimilarProductsAsync(
                            action.ProductId,
                            action.ProductType,
                            2);
                        recommendations.AddRange(similar);
                    }

                    if (recommendations.Count < limit)
                    {
                        var needed = limit - recommendations.Count;
                        var popular = await GetPopularItemsAsync(null, needed);
                        recommendations.AddRange(popular);
                    }

                    return recommendations.Take(limit).ToList();
                }

                return await GetPopularItemsAsync(null, limit);
            }) ?? new List<RecommendationItem>();
        }

        public async Task<List<RecommendationItem>> GetPersonalRecommendationsAsync(int userId, int limit = 10)
        {
            var cacheKey = $"{CACHE_PREFIX}personal_{userId}_{limit}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

                // Пытаемся получить историю из памяти
                if (_userActions.TryGetValue($"user_{userId}", out var actions) && actions.Any())
                {
                    var recentActions = actions
                        .Where(a => a.ActionType == UserActionType.View ||
                                   a.ActionType == UserActionType.AddToCart ||
                                   a.ActionType == UserActionType.Purchase)
                        .OrderByDescending(a => a.Timestamp)
                        .Take(5)
                        .ToList();

                    var recommendations = new List<RecommendationItem>();

                    foreach (var action in recentActions)
                    {
                        var similar = await GetSimilarProductsAsync(
                            action.ProductId,
                            action.ProductType,
                            2);
                        recommendations.AddRange(similar);
                    }

                    if (recommendations.Count < limit)
                    {
                        var needed = limit - recommendations.Count;
                        var popular = await GetPopularItemsAsync(null, needed);
                        recommendations.AddRange(popular);
                    }

                    return recommendations.Take(limit).ToList();
                }

                // Если нет истории в памяти, загружаем из JSON
                var jsonActions = await LoadActionsFromJsonAsync();
                var userActions = jsonActions
                    .Where(a => a.UserId == userId &&
                              (a.ActionType == UserActionType.View ||
                               a.ActionType == UserActionType.AddToCart ||
                               a.ActionType == UserActionType.Purchase))
                    .OrderByDescending(a => a.Timestamp)
                    .Take(5)
                    .ToList();

                if (userActions.Any())
                {
                    var recommendations = new List<RecommendationItem>();

                    foreach (var action in userActions)
                    {
                        var similar = await GetSimilarProductsAsync(
                            action.ProductId,
                            action.ProductType,
                            2);
                        recommendations.AddRange(similar);
                    }

                    if (recommendations.Count < limit)
                    {
                        var needed = limit - recommendations.Count;
                        var popular = await GetPopularItemsAsync(null, needed);
                        recommendations.AddRange(popular);
                    }

                    return recommendations.Take(limit).ToList();
                }

                return await GetPopularItemsAsync(null, limit);
            }) ?? new List<RecommendationItem>();
        }

        public async Task CleanupOldDataAsync()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-ACTION_CLEANUP_HOURS);

                // Очищаем действия в памяти
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

                // Очищаем JSON файл
                await CleanupOldActionsInJsonAsync();

                _logger.LogInformation("Cleaned up old recommendation data");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up recommendation data");
            }
        }

        private async Task CleanupOldActionsInJsonAsync()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, ACTIONS_FILE);
                if (!File.Exists(filePath)) return;

                var cutoff = DateTime.UtcNow.AddHours(-ACTION_CLEANUP_HOURS * 2); // Дольше храним в JSON

                var json = await File.ReadAllTextAsync(filePath);
                var actions = JsonSerializer.Deserialize<List<UserAction>>(json) ?? new List<UserAction>();

                var filtered = actions.Where(a => a.Timestamp >= cutoff).ToList();

                var serialized = JsonSerializer.Serialize(filtered, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(filePath, serialized);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up actions in JSON");
            }
        }

        // Вспомогательные методы для загрузки данных из JSON
        private async Task<List<Computer>> LoadComputersAsync()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "computers.json");
                if (!File.Exists(filePath)) return new List<Computer>();

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<Computer>>(json) ?? new List<Computer>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading computers from JSON");
                return new List<Computer>();
            }
        }

        private async Task<List<Component>> LoadComponentsAsync()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "components.json");
                if (!File.Exists(filePath)) return new List<Component>();

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<Component>>(json) ?? new List<Component>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading components from JSON");
                return new List<Component>();
            }
        }

        private async Task<List<UserAction>> LoadActionsFromJsonAsync()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, ACTIONS_FILE);
                if (!File.Exists(filePath)) return new List<UserAction>();

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<UserAction>>(json) ?? new List<UserAction>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading actions from JSON");
                return new List<UserAction>();
            }
        }

        private string GetActionStorageKey(UserAction action)
        {
            return action.UserId.HasValue
                ? $"user_{action.UserId}"
                : $"guest_{action.GuestId}";
        }

        private async Task InvalidateUserCacheAsync(int? userId, string guestId)
        {
            try
            {
                var keysToRemove = new List<string>();

                if (userId.HasValue)
                {
                    keysToRemove.Add($"{CACHE_PREFIX}personal_{userId}_");
                    keysToRemove.Add($"{CACHE_PREFIX}rec_{userId}_");
                }

                if (!string.IsNullOrEmpty(guestId))
                {
                    keysToRemove.Add($"{CACHE_PREFIX}guest_{guestId}_");
                    keysToRemove.Add($"{CACHE_PREFIX}rec_{guestId}_");
                }

                // Удаляем все ключи, начинающиеся с префиксов
                foreach (var keyPrefix in keysToRemove)
                {
                    // В реальном приложении используйте Redis или другой распределенный кэш
                    // с поддержкой удаления по префиксу
                    _cache.Remove(keyPrefix + "*");
                }

                _logger.LogDebug("Invalidating cache for user/guest");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache");
            }
        }

        private void StartPeriodicCleanup()
        {
            var cleanupTimer = new Timer(async _ =>
            {
                try
                {
                    await CleanupOldDataAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in periodic cleanup");
                }
            }, null, TimeSpan.FromHours(6), TimeSpan.FromHours(6));

            _appLifetime.ApplicationStopping.Register(() =>
            {
                cleanupTimer?.Dispose();
            });
        }
    }
}