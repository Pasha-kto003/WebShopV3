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
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RecommendationService> _logger;
        private readonly CompatibilityService _compatibilityService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHostApplicationLifetime _appLifetime;

        private readonly ConcurrentDictionary<string, List<UserAction>> _userActions = new();
        private readonly ConcurrentDictionary<string, List<UserAction>> _guestActions = new();

        private const int MAX_ACTIONS_PER_USER = 500;
        private const int RECOMMENDATION_CACHE_MINUTES = 10;
        private const int ACTION_CLEANUP_HOURS = 24;
        private const string CACHE_PREFIX = "rec_";

        public RecommendationService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<RecommendationService> logger,
            CompatibilityService compatibilityService,
            IHttpContextAccessor httpContextAccessor,
            IHostApplicationLifetime appLifetime)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _compatibilityService = compatibilityService;
            _httpContextAccessor = httpContextAccessor;
            _appLifetime = appLifetime;

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

                await InvalidateUserCacheAsync(action.UserId, action.GuestId);

                _logger.LogDebug("Action tracked: {UserId}/{GuestId} - {ActionType} - {ProductId}",
                    action.UserId, action.GuestId, action.ActionType, action.ProductId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking action");
            }
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
            var computer = await _context.Computers
                .Include(c => c.ComputerComponents)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == computerId);

            if (computer == null) return new List<RecommendationItem>();

            var currentComponents = computer.ComputerComponents.Select(cc => cc.ComponentId).ToList();
            var currentPrice = computer.Price;

            var similarComputers = await _context.Computers
                .Include(c => c.ComputerComponents)
                .Where(c => c.Id != computerId && c.Quantity > 0)
                .AsNoTracking()
                .Select(c => new
                {
                    Computer = c,
                    CommonComponents = c.ComputerComponents.Count(cc => currentComponents.Contains(cc.ComponentId)),
                    PriceDifference = Math.Abs(c.Price - currentPrice)
                })
                .OrderByDescending(x => x.CommonComponents)
                .ThenBy(x => x.PriceDifference)
                .Take(limit * 2)
                .ToListAsync();

            var recommendations = new List<RecommendationItem>();
            var random = new Random();

            foreach (var item in similarComputers)
            {
                // Используем decimal для расчетов, затем конвертируем в double
                var componentScore = currentComponents.Count > 0
                    ? (double)item.CommonComponents / currentComponents.Count * 0.6
                    : 0;

                var priceScore = (double)(1 - Math.Min(item.PriceDifference / currentPrice, 1)) * 0.4;
                var totalScore = componentScore + priceScore;

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
                        { "common_components", (double)item.CommonComponents / Math.Max(currentComponents.Count, 1) },
                        { "price_similarity", (double)(1 - Math.Min(item.PriceDifference / currentPrice, 1)) }
                    }
                });
            }

            return recommendations
                .OrderByDescending(r => r.RelevanceScore)
                .Take(limit)
                .ToList();
        }

        private async Task<List<RecommendationItem>> FindSimilarComponentsAsync(int componentId, int limit)
        {
            var component = await _context.Components
                .Include(c => c.ComponentCharacteristics)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == componentId);

            if (component == null) return new List<RecommendationItem>();

            var componentType = component.Type ?? "";
            var currentPrice = component.Price;

            var similarComponents = await _context.Components
                .Where(c => c.Id != componentId &&
                           c.Type == componentType &&
                           c.Quantity > 0)
                .AsNoTracking()
                .Select(c => new
                {
                    Component = c,
                    PriceDifference = Math.Abs(c.Price - currentPrice)
                })
                .OrderBy(x => x.PriceDifference)
                .ThenByDescending(c => c.Component.Quantity)
                .Take(limit * 2)
                .ToListAsync();

            var recommendations = new List<RecommendationItem>();
            var random = new Random();

            foreach (var item in similarComponents)
            {
                // Конвертируем decimal в double
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
                    var computer = await _context.Computers
                        .Include(c => c.ComputerComponents)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == firstComputerId);

                    if (computer != null)
                    {
                        var computerComponents = computer.ComputerComponents.Select(cc => cc.ComponentId).ToList();

                        // Ищем компоненты такого же типа
                        var componentTypes = await _context.Components
                            .Where(c => computerComponents.Contains(c.Id))
                            .Select(c => c.Type)
                            .Distinct()
                            .ToListAsync();

                        if (componentTypes.Any())
                        {
                            var additionalComponents = await _context.Components
                                .Where(c => componentTypes.Contains(c.Type) && c.Quantity > 0)
                                .OrderBy(c => Guid.NewGuid())
                                .Take(3)
                                .AsNoTracking()
                                .ToListAsync();

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
                }

                // Добавляем популярные
                if (recommendations.Count < 6)
                {
                    var needed = 6 - recommendations.Count;
                    var popular = await GetPopularItemsAsync(null, needed);
                    recommendations.AddRange(popular);
                }

                // Убираем дубликаты
                return recommendations
                    .Where(r => !cart.Items.Any(i =>
                        (i.IsComputer && i.ComputerId == r.Id && r.ProductType == "Computer") ||
                        (i.IsComponent && i.ComponentId == r.Id && r.ProductType == "Component")))
                    .Take(6)
                    .ToList();
            }) ?? new List<RecommendationItem>();
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
                    var popularComputers = await _context.Computers
                        .Where(c => c.Quantity > 0)
                        .OrderByDescending(c => c.Id)
                        .Take(limit)
                        .AsNoTracking()
                        .ToListAsync();

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
                    var popularComponents = await _context.Components
                        .Where(c => c.Quantity > 0)
                        .OrderByDescending(c => c.Id)
                        .Take(limit)
                        .AsNoTracking()
                        .ToListAsync();

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

                // Пытаемся получить историю
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

                return await GetPopularItemsAsync(null, limit);
            }) ?? new List<RecommendationItem>();
        }

        public async Task<List<RecommendationItem>> GetPersonalRecommendationsAsync(int userId, int limit = 10)
        {
            var cacheKey = $"{CACHE_PREFIX}personal_{userId}_{limit}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

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

                    // Добавляем популярные
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

                // Очищаем действия пользователей
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

                // Очищаем действия гостей
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

                _logger.LogInformation("Cleaned up old recommendation data");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up recommendation data");
            }
        }

        // Вспомогательные методы
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