// Services/FavoriteService.cs
using System.Text.Json;
using WebShopV3.Json.Models;

namespace WebShopV3.Json.Services
{
    public interface IFavoriteService
    {
        Task<FavoriteResult> AddToFavoritesAsync(int? userId, string guestId, string productType, int productId);
        Task<FavoriteResult> RemoveFromFavoritesAsync(int? userId, string guestId, int favoriteId);
        Task<FavoriteResult> RemoveByProductAsync(int? userId, string guestId, string productType, int productId);
        Task<List<Favorite>> GetFavoritesAsync(int? userId, string guestId);
        Task<List<FavoriteWithProduct>> GetFavoritesWithProductsAsync(int? userId, string guestId);
        Task<int> GetFavoriteCountAsync(int? userId, string guestId);
        Task<bool> IsProductInFavoritesAsync(int? userId, string guestId, string productType, int productId);
        Task<FavoriteResult> MigrateGuestFavoritesAsync(string guestId, int userId);
        Task CleanupOldGuestFavoritesAsync();
    }

    public class FavoriteService : IFavoriteService
    {
        private readonly JsonDataService _jsonData;
        private readonly ILogger<FavoriteService> _logger;
        private const int MAX_GUEST_FAVORITES = 5;
        private const int MAX_USER_FAVORITES = 20;
        private const string FAVORITES_FILE = "favorites.json";

        public FavoriteService(JsonDataService jsonData, ILogger<FavoriteService> logger)
        {
            _jsonData = jsonData;
            _logger = logger;
        }

        private string GetFilePath()
        {
            return Path.Combine(_jsonData.DataPath, FAVORITES_FILE);
        }

        private async Task<List<Favorite>> LoadFavoritesAsync()
        {
            try
            {
                var filePath = GetFilePath();

                if (!File.Exists(filePath))
                    return new List<Favorite>();

                var json = await File.ReadAllTextAsync(filePath);
                var favorites = JsonSerializer.Deserialize<List<Favorite>>(json) ?? new List<Favorite>();

                // Удаляем старые записи с пустыми ID
                favorites.RemoveAll(f => f.Id == 0);

                return favorites;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке избранного из JSON");
                return new List<Favorite>();
            }
        }

        private async Task SaveFavoritesAsync(List<Favorite> favorites)
        {
            try
            {
                var filePath = Path.Combine(_jsonData.DataPath, FAVORITES_FILE);
                var json = JsonSerializer.Serialize(favorites, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении избранного в JSON");
                throw;
            }
        }

        private int GetNextFavoriteId(List<Favorite> favorites)
        {
            if (!favorites.Any())
                return 1;

            return favorites.Max(f => f.Id) + 1;
        }

        public async Task<FavoriteResult> AddToFavoritesAsync(int? userId, string guestId, string productType, int productId)
        {
            try
            {
                var favorites = await LoadFavoritesAsync();

                // Проверка на дубликат
                var existingFavorite = favorites.FirstOrDefault(f =>
                    (userId.HasValue ? f.UserId == userId : f.GuestId == guestId) &&
                    f.ProductType == productType &&
                    f.ProductId == productId);

                if (existingFavorite != null)
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = "Товар уже в избранном"
                    };
                }

                // Проверка лимита
                var userFavorites = favorites.Where(f =>
                    userId.HasValue ? f.UserId == userId : f.GuestId == guestId).ToList();

                var maxFavorites = userId.HasValue ? MAX_USER_FAVORITES : MAX_GUEST_FAVORITES;

                if (userFavorites.Count >= maxFavorites)
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = $"Лимит избранного достигнут (максимум {maxFavorites} товаров)"
                    };
                }

                // Создание новой записи
                var favorite = new Favorite
                {
                    Id = GetNextFavoriteId(favorites),
                    UserId = userId,
                    GuestId = userId.HasValue ? null : guestId,
                    ProductType = productType,
                    ProductId = productId,
                    AddedAt = DateTime.UtcNow,
                    LastViewed = DateTime.UtcNow
                };

                favorites.Add(favorite);
                await SaveFavoritesAsync(favorites);

                return new FavoriteResult
                {
                    Success = true,
                    Message = "Товар добавлен в избранное",
                    TotalCount = userFavorites.Count + 1,
                    Favorite = favorite
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении в избранное");
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Ошибка при добавлении в избранное"
                };
            }
        }

        public async Task<FavoriteResult> RemoveFromFavoritesAsync(int? userId, string guestId, int favoriteId)
        {
            try
            {
                var favorites = await LoadFavoritesAsync();

                var favorite = favorites.FirstOrDefault(f =>
                    f.Id == favoriteId &&
                    (userId.HasValue ? f.UserId == userId : f.GuestId == guestId));

                if (favorite == null)
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = "Товар не найден в избранном"
                    };
                }

                favorites.Remove(favorite);
                await SaveFavoritesAsync(favorites);

                var newCount = await GetFavoriteCountAsync(userId, guestId);

                return new FavoriteResult
                {
                    Success = true,
                    Message = "Товар удален из избранного",
                    TotalCount = newCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении из избранного");
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Ошибка при удалении из избранного"
                };
            }
        }

        public async Task<FavoriteResult> RemoveByProductAsync(int? userId, string guestId, string productType, int productId)
        {
            try
            {
                var favorites = await LoadFavoritesAsync();

                var favorite = favorites.FirstOrDefault(f =>
                    (userId.HasValue ? f.UserId == userId : f.GuestId == guestId) &&
                    f.ProductType == productType &&
                    f.ProductId == productId);

                if (favorite == null)
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = "Товар не найден в избранном"
                    };
                }

                favorites.Remove(favorite);
                await SaveFavoritesAsync(favorites);

                var newCount = await GetFavoriteCountAsync(userId, guestId);

                return new FavoriteResult
                {
                    Success = true,
                    Message = "Товар удален из избранного",
                    TotalCount = newCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении из избранного");
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Ошибка при удалении из избранного"
                };
            }
        }

        public async Task<List<Favorite>> GetFavoritesAsync(int? userId, string guestId)
        {
            try
            {
                var favorites = await LoadFavoritesAsync();

                return favorites
                    .Where(f => userId.HasValue ? f.UserId == userId : f.GuestId == guestId)
                    .OrderByDescending(f => f.AddedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранного");
                return new List<Favorite>();
            }
        }

        public async Task<List<FavoriteWithProduct>> GetFavoritesWithProductsAsync(int? userId, string guestId)
        {
            try
            {
                var favorites = await GetFavoritesAsync(userId, guestId);
                var result = new List<FavoriteWithProduct>();

                // Разделяем компьютеры и компоненты для загрузки
                var computerFavorites = favorites.Where(f => f.ProductType == "Computer").ToList();
                var componentFavorites = favorites.Where(f => f.ProductType == "Component").ToList();

                // Загружаем компьютеры
                if (computerFavorites.Any())
                {
                    var computerIds = computerFavorites.Select(f => f.ProductId).ToList();
                    var computers = await _jsonData.GetAllAsync<Computer>("computers");
                    var computerDict = computers
                        .Where(c => computerIds.Contains(c.Id))
                        .ToDictionary(c => c.Id);

                    foreach (var favorite in computerFavorites)
                    {
                        if (computerDict.TryGetValue(favorite.ProductId, out var computer))
                        {
                            result.Add(new FavoriteWithProduct
                            {
                                Favorite = favorite,
                                Computer = computer,
                                Component = null
                            });
                        }
                    }
                }

                // Загружаем компоненты
                if (componentFavorites.Any())
                {
                    var componentIds = componentFavorites.Select(f => f.ProductId).ToList();
                    var components = await _jsonData.GetAllAsync<Component>("components");
                    var componentDict = components
                        .Where(c => componentIds.Contains(c.Id))
                        .ToDictionary(c => c.Id);

                    foreach (var favorite in componentFavorites)
                    {
                        if (componentDict.TryGetValue(favorite.ProductId, out var component))
                        {
                            result.Add(new FavoriteWithProduct
                            {
                                Favorite = favorite,
                                Computer = null,
                                Component = component
                            });
                        }
                    }
                }

                // Сортируем по дате добавления
                return result.OrderByDescending(r => r.Favorite.AddedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранного с продуктами");
                return new List<FavoriteWithProduct>();
            }
        }

        public async Task<int> GetFavoriteCountAsync(int? userId, string guestId)
        {
            try
            {
                var favorites = await LoadFavoritesAsync();

                return favorites.Count(f =>
                    userId.HasValue ? f.UserId == userId : f.GuestId == guestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подсчете избранного");
                return 0;
            }
        }

        public async Task<bool> IsProductInFavoritesAsync(int? userId, string guestId, string productType, int productId)
        {
            try
            {
                var favorites = await LoadFavoritesAsync();

                return favorites.Any(f =>
                    (userId.HasValue ? f.UserId == userId : f.GuestId == guestId) &&
                    f.ProductType == productType &&
                    f.ProductId == productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке избранного");
                return false;
            }
        }

        public async Task<FavoriteResult> MigrateGuestFavoritesAsync(string guestId, int userId)
        {
            try
            {
                var favorites = await LoadFavoritesAsync();

                // Находим все избранные гостя
                var guestFavorites = favorites
                    .Where(f => f.GuestId == guestId)
                    .ToList();

                if (!guestFavorites.Any())
                {
                    return new FavoriteResult
                    {
                        Success = true,
                        Message = "Нет гостевых избранных для миграции"
                    };
                }

                // Проверяем лимит пользователя
                var userFavoriteCount = favorites.Count(f => f.UserId == userId);
                var availableSlots = MAX_USER_FAVORITES - userFavoriteCount;

                if (availableSlots <= 0)
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = $"Лимит избранного ({MAX_USER_FAVORITES} товаров) уже достигнут"
                    };
                }

                // Переносим, учитывая лимит
                var favoritesToMigrate = guestFavorites
                    .Take(availableSlots)
                    .ToList();

                int migratedCount = 0;
                int duplicateCount = 0;

                foreach (var favorite in favoritesToMigrate.ToList())
                {
                    // Проверяем, нет ли уже такого товара у пользователя
                    var existing = favorites.FirstOrDefault(f =>
                        f.UserId == userId &&
                        f.ProductType == favorite.ProductType &&
                        f.ProductId == favorite.ProductId);

                    if (existing == null)
                    {
                        // Мигрируем запись
                        favorite.UserId = userId;
                        favorite.GuestId = null;
                        migratedCount++;
                    }
                    else
                    {
                        // Если уже есть, удаляем гостевую запись
                        favorites.Remove(favorite);
                        guestFavorites.Remove(favorite);
                        duplicateCount++;
                    }
                }

                // Удаляем оставшиеся (не поместившиеся в лимит) гостевые записи
                var remainingGuestFavorites = guestFavorites
                    .Except(favoritesToMigrate)
                    .ToList();

                foreach (var favorite in remainingGuestFavorites)
                {
                    favorites.Remove(favorite);
                }

                await SaveFavoritesAsync(favorites);

                var message = $"Мигрировано {migratedCount} товаров";
                if (duplicateCount > 0)
                {
                    message += $", {duplicateCount} дубликатов удалено";
                }
                if (remainingGuestFavorites.Any())
                {
                    message += $", {remainingGuestFavorites.Count} не поместилось из-за лимита";
                }

                return new FavoriteResult
                {
                    Success = true,
                    Message = message,
                    TotalCount = await GetFavoriteCountAsync(userId, null)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка миграции избранного для гостя {guestId} -> пользователя {userId}");
                return new FavoriteResult
                {
                    Success = false,
                    Message = "Ошибка миграции избранного"
                };
            }
        }

        public async Task CleanupOldGuestFavoritesAsync()
        {
            try
            {
                var favorites = await LoadFavoritesAsync();

                // Удаляем записи гостей старше 30 дней
                var cutoffDate = DateTime.UtcNow.AddDays(-30);

                var oldFavorites = favorites
                    .Where(f => f.GuestId != null && f.LastViewed < cutoffDate)
                    .ToList();

                if (oldFavorites.Any())
                {
                    foreach (var favorite in oldFavorites)
                    {
                        favorites.Remove(favorite);
                    }

                    await SaveFavoritesAsync(favorites);
                    _logger.LogInformation($"Очищено {oldFavorites.Count} старых гостевых избранных");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка очистки старых гостевых избранных");
            }
        }
    }

    // Модели для работы с избранным
    public class Favorite
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? GuestId { get; set; }
        public string ProductType { get; set; } = string.Empty; // "Computer" или "Component"
        public int ProductId { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime LastViewed { get; set; }
    }

    public class FavoriteWithProduct
    {
        public Favorite Favorite { get; set; } = null!;
        public Computer? Computer { get; set; }
        public Component? Component { get; set; }
    }

    public class FavoriteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public Favorite? Favorite { get; set; }
    }
}