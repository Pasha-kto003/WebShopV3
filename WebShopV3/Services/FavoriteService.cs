// Services/FavoriteService.cs
using Microsoft.EntityFrameworkCore;
using WebShopV3.Models;

namespace WebShopV3.Services
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
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FavoriteService> _logger;
        private const int MAX_GUEST_FAVORITES = 5;
        private const int MAX_USER_FAVORITES = 20;

        public FavoriteService(ApplicationDbContext context, ILogger<FavoriteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Services/FavoriteService.cs - временный тестовый метод
        public async Task<FavoriteResult> AddToFavoritesAsync(int? userId, string guestId, string productType, int productId)
        {
            try
            {
                // ПРОСТАЯ ПРОВЕРКА - временно убираем сложные проверки
                var favorite = new Favorite
                {
                    UserId = userId,
                    GuestId = userId.HasValue ? "" : guestId, // Для пользователей GuestId = null
                    ProductType = productType,
                    ProductId = productId,
                    AddedAt = DateTime.UtcNow,
                    LastViewed = DateTime.UtcNow
                };

                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();

                return new FavoriteResult
                {
                    Success = true,
                    Message = "Товар добавлен в избранное",
                    TotalCount = await GetFavoriteCountAsync(userId, guestId),
                    Favorite = favorite
                };
            }
            catch (DbUpdateException dbEx)
            {
                // Логируем конкретную ошибку БД
                _logger.LogError(dbEx, "Ошибка БД при добавлении в избранное. UserId: {UserId}, ProductType: {ProductType}, ProductId: {ProductId}",
                    userId, productType, productId);

                // Проверяем, может быть дубликат
                if (dbEx.InnerException?.Message.Contains("duplicate") == true ||
                    dbEx.InnerException?.Message.Contains("unique") == true)
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = "Товар уже в избранном"
                    };
                }

                return new FavoriteResult
                {
                    Success = false,
                    Message = $"Ошибка базы данных: {dbEx.InnerException?.Message ?? dbEx.Message}"
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
                var favorite = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.Id == favoriteId &&
                        (f.UserId == userId || f.GuestId == guestId));

                if (favorite == null)
                {
                    return new FavoriteResult
                    {
                        Success = false,
                        Message = "Товар не найден в избранном"
                    };
                }

                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();

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
                var favorite = await _context.Favorites
                    .FirstOrDefaultAsync(f =>
                        (f.UserId == userId || f.GuestId == guestId) &&
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

                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();

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
                return await _context.Favorites
                    .Where(f => f.UserId == userId || f.GuestId == guestId)
                    .OrderByDescending(f => f.AddedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранного");
                return new List<Favorite>();
            }
        }

        // Services/FavoriteService.cs - исправить метод
        // Services/FavoriteService.cs - обновить метод
        public async Task<List<FavoriteWithProduct>> GetFavoritesWithProductsAsync(int? userId, string guestId)
        {
            try
            {
                var favorites = await GetFavoritesAsync(userId, guestId);
                var result = new List<FavoriteWithProduct>();

                // Разделяем компьютеры и компоненты для batch-запросов
                var computerFavorites = favorites.Where(f => f.ProductType == "Computer").ToList();
                var componentFavorites = favorites.Where(f => f.ProductType == "Component").ToList();

                // Загружаем компьютеры одним запросом
                if (computerFavorites.Any())
                {
                    var computerIds = computerFavorites.Select(f => f.ProductId).ToList();
                    var computers = await _context.Computers
                        .Where(c => computerIds.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id);

                    foreach (var favorite in computerFavorites)
                    {
                        if (computers.TryGetValue(favorite.ProductId, out var computer))
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

                // Загружаем компоненты одним запросом
                if (componentFavorites.Any())
                {
                    var componentIds = componentFavorites.Select(f => f.ProductId).ToList();
                    var components = await _context.Components
                        .Where(c => componentIds.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id);

                    foreach (var favorite in componentFavorites)
                    {
                        if (components.TryGetValue(favorite.ProductId, out var component))
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
                return await _context.Favorites
                    .Where(f => f.UserId == userId || f.GuestId == guestId)
                    .CountAsync();
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
                return await _context.Favorites
                    .AnyAsync(f =>
                        (f.UserId == userId || f.GuestId == guestId) &&
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
                // Находим все избранные гостя
                var guestFavorites = await _context.Favorites
                    .Where(f => f.GuestId == guestId)
                    .ToListAsync();

                if (!guestFavorites.Any())
                {
                    return new FavoriteResult
                    {
                        Success = true,
                        Message = "Нет гостевых избранных для миграции"
                    };
                }

                // Проверяем лимит пользователя
                var userFavoriteCount = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .CountAsync();

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

                foreach (var favorite in favoritesToMigrate)
                {
                    // Проверяем, нет ли уже такого товара у пользователя
                    var existing = await _context.Favorites
                        .FirstOrDefaultAsync(f =>
                            f.UserId == userId &&
                            f.ProductType == favorite.ProductType &&
                            f.ProductId == favorite.ProductId);

                    if (existing == null)
                    {
                        favorite.UserId = userId;
                        favorite.GuestId = null;
                        migratedCount++;
                    }
                    else
                    {
                        // Если уже есть, удаляем гостевую запись
                        _context.Favorites.Remove(favorite);
                        duplicateCount++;
                    }
                }

                await _context.SaveChangesAsync();

                var message = $"Мигрировано {migratedCount} товаров";
                if (duplicateCount > 0)
                {
                    message += $", {duplicateCount} дубликатов удалено";
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
                // Удаляем записи гостей старше 30 дней
                var cutoffDate = DateTime.UtcNow.AddDays(-30);

                var oldFavorites = await _context.Favorites
                    .Where(f => f.GuestId != null && f.LastViewed < cutoffDate)
                    .ToListAsync();

                if (oldFavorites.Any())
                {
                    _context.Favorites.RemoveRange(oldFavorites);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Очищено {oldFavorites.Count} старых гостевых избранных");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка очистки старых гостевых избранных");
            }
        }
    }
}