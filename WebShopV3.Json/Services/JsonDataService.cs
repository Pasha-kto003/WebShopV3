using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebShopV3.Json.Models;

namespace WebShopV3.Json.Services
{
    public class JsonDataService
    {
        private readonly string _dataPath;
        private readonly ILogger<JsonDataService> _logger;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.IgnoreCycles, // Игнорировать циклические ссылки
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Кэш для сопоставления имен сущностей с реальными именами файлов
        private static readonly ConcurrentDictionary<string, string> _filePathCache =
            new(StringComparer.OrdinalIgnoreCase);

        // Кэш содержимого файлов для производительности
        private static readonly ConcurrentDictionary<string, object> _fileContentCache =
            new(StringComparer.OrdinalIgnoreCase);

        // Блокировки для безопасного доступа к файлам
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private string[]? _existingFiles;
        private DateTime _lastCacheRefresh = DateTime.MinValue;
        private const int CACHE_REFRESH_MINUTES = 5;

        public JsonDataService(ILogger<JsonDataService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _dataPath = Path.Combine(env.ContentRootPath, "Data", "Json");

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
                _logger.LogInformation("Создана директория для JSON данных: {DataPath}", _dataPath);
            }

            // Инициализируем список существующих файлов
            RefreshExistingFiles();

            _logger.LogDebug("JsonDataService инициализирован. Путь: {DataPath}", _dataPath);
        }

        public string DataPath => _dataPath;

        private void RefreshExistingFiles(bool force = false)
        {
            try
            {
                // Обновляем кэш только если прошло достаточно времени или принудительно
                if (!force && (DateTime.UtcNow - _lastCacheRefresh).TotalMinutes < CACHE_REFRESH_MINUTES)
                    return;

                if (Directory.Exists(_dataPath))
                {
                    _existingFiles = Directory.GetFiles(_dataPath, "*.json")
                        .Select(f => Path.GetFileName(f))
                        .ToArray();

                    _lastCacheRefresh = DateTime.UtcNow;

                    _logger.LogDebug("Обновлен список файлов. Найдено: {Count} файлов",
                        _existingFiles.Length);
                }
                else
                {
                    _existingFiles = Array.Empty<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сканировании файлов JSON");
                _existingFiles = Array.Empty<string>();
            }
        }

        private string GetFilePath(string entityName, bool createIfNotExists = false)
        {
            // Приводим к стандартному виду (нижний регистр, без пробелов)
            var normalizedName = NormalizeEntityName(entityName);

            // Проверяем кэш сначала
            if (_filePathCache.TryGetValue(normalizedName, out var cachedPath))
                return cachedPath;

            // Ищем существующий файл
            string? actualFileName = null;

            if (_existingFiles != null)
            {
                // Ищем точное совпадение (с учетом .json расширения)
                var targetFileName = $"{normalizedName}.json";
                actualFileName = _existingFiles.FirstOrDefault(f =>
                    string.Equals(f, targetFileName, StringComparison.OrdinalIgnoreCase));

                // Если не нашли, ищем частичное совпадение
                if (actualFileName == null)
                {
                    actualFileName = _existingFiles.FirstOrDefault(f =>
                        f.StartsWith(normalizedName, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Если файл не найден и нужно создать
            if (actualFileName == null)
            {
                if (createIfNotExists)
                {
                    actualFileName = $"{normalizedName}.json";
                }
                else
                {
                    // Возвращаем ожидаемый путь (для логирования ошибок)
                    actualFileName = $"{normalizedName}.json";
                }
            }

            var filePath = Path.Combine(_dataPath, actualFileName);

            // Сохраняем в кэш
            _filePathCache[normalizedName] = filePath;

            return filePath;
        }

        private string NormalizeEntityName(string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Имя сущности не может быть пустым", nameof(entityName));

            // Приводим к нижнему регистру, удаляем пробелы и специальные символы
            return entityName.Trim().ToLowerInvariant();
        }

        private void InvalidateCache(string entityName)
        {
            var normalizedName = NormalizeEntityName(entityName);
            _filePathCache.TryRemove(normalizedName, out _);

            // Также инвалидируем кэш содержимого
            var cacheKey = $"content_{normalizedName}";
            _fileContentCache.TryRemove(cacheKey, out _);

            // Обновляем список файлов
            RefreshExistingFiles(true);
        }

        public async Task<List<T>> GetAllAsync<T>(string entityName)
        {
            var filePath = GetFilePath(entityName);
            var cacheKey = $"content_{NormalizeEntityName(entityName)}";

            // Проверяем кэш содержимого
            if (_fileContentCache.TryGetValue(cacheKey, out var cached) && cached is List<T> cachedList)
            {
                _logger.LogDebug("Возвращаем данные {EntityName} из кэша ({Count} записей)",
                    entityName, cachedList.Count);
                return cachedList;
            }

            try
            {
                _logger.LogDebug("Загрузка {EntityName} из {FilePath}", entityName, filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Файл не найден: {FilePath}", filePath);
                    return new List<T>();
                }

                var json = await File.ReadAllTextAsync(filePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Файл пуст: {FilePath}", filePath);
                    return new List<T>();
                }

                List<T>? result;

                try
                {
                    // Для заказов используем специальную обработку
                    if (entityName.Equals("orders", StringComparison.OrdinalIgnoreCase) && typeof(T) == typeof(Order))
                    {
                        result = await DeserializeOrdersWithFixAsync(json) as List<T>;
                    }
                    else
                    {
                        result = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx,
                        "Ошибка десериализации JSON файла {FilePath}", filePath);

                    // Пробуем восстановить файл
                    await TryRepairJsonFileAsync(filePath, json);
                    return new List<T>();
                }

                if (result == null)
                {
                    _logger.LogWarning("Не удалось десериализовать файл: {FilePath}", filePath);
                    return new List<T>();
                }

                _logger.LogDebug("Загружено {Count} записей из {EntityName}", result.Count, entityName);

                // Для заказов исправляем связи
                if (entityName.Equals("orders", StringComparison.OrdinalIgnoreCase) && typeof(T) == typeof(Order))
                {
                    await FixOrderRelationshipsAsync(result as List<Models.Order>);
                }

                // Сохраняем в кэш
                _fileContentCache[cacheKey] = result;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении {EntityName} из JSON", entityName);
                return new List<T>();
            }
        }

        private async Task<List<Models.Order>> DeserializeOrdersWithFixAsync(string json)
        {
            try
            {
                // Сначала десериализуем в промежуточный тип
                using var document = JsonDocument.Parse(json);
                var orders = new List<Models.Order>();

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    try
                    {
                        var order = new Models.Order();

                        // Читаем простые свойства
                        if (element.TryGetProperty("id", out var idProp))
                            order.Id = idProp.GetInt32();

                        if (element.TryGetProperty("orderDate", out var dateProp))
                            order.OrderDate = dateProp.GetDateTime();

                        if (element.TryGetProperty("totalAmount", out var amountProp))
                            order.TotalAmount = amountProp.GetDecimal();

                        if (element.TryGetProperty("userId", out var userIdProp))
                            order.UserId = userIdProp.GetInt32();

                        if (element.TryGetProperty("orderTypeId", out var orderTypeIdProp))
                            order.OrderTypeId = orderTypeIdProp.GetInt32();

                        if (element.TryGetProperty("statusId", out var statusIdProp))
                            order.StatusId = statusIdProp.GetInt32();

                        if (element.TryGetProperty("description", out var descProp))
                            order.Description = descProp.GetString();

                        // Обрабатываем возможный объект Status
                        if (element.TryGetProperty("status", out var statusProp))
                        {
                            if (statusProp.ValueKind == JsonValueKind.Object)
                            {
                                // Если Status это объект, извлекаем ID
                                if (statusProp.TryGetProperty("id", out var statusIdProp2))
                                {
                                    order.StatusId = statusIdProp2.GetInt32();
                                }
                            }
                            else if (statusProp.ValueKind == JsonValueKind.Number)
                            {
                                order.StatusId = statusProp.GetInt32();
                            }
                            else if (statusProp.ValueKind == JsonValueKind.String)
                            {
                                // Если Status это строка (имя), ищем соответствующий ID
                                var statusName = statusProp.GetString();
                                if (!string.IsNullOrEmpty(statusName))
                                {
                                    var statuses = await GetAllAsync<Status>("OrderStatuses");
                                    var status = statuses.FirstOrDefault(s => s.Name == statusName);
                                    if (status != null)
                                    {
                                        order.StatusId = status.Id;
                                    }
                                }
                            }
                        }

                        // Аналогично для OrderType
                        if (element.TryGetProperty("orderType", out var orderTypeProp))
                        {
                            if (orderTypeProp.ValueKind == JsonValueKind.Object)
                            {
                                if (orderTypeProp.TryGetProperty("id", out var orderTypeIdProp2))
                                {
                                    order.OrderTypeId = orderTypeIdProp2.GetInt32();
                                }
                            }
                        }

                        orders.Add(order);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ошибка при десериализации элемента заказа");
                        continue;
                    }
                }

                return orders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке файла заказов");
                return new List<Models.Order>();
            }
        }

        private async Task FixOrderRelationshipsAsync(List<Models.Order> orders)
        {
            try
            {
                if (!orders.Any()) return;

                // Загружаем связанные данные
                var statuses = await GetAllAsync<Status>("orderStatuses");
                var orderTypes = await GetAllAsync<OrderType>("ordertypes");
                var users = await GetAllAsync<User>("users");
                var computerOrders = await GetAllAsync<ComputerOrder>("computerorders");
                var componentOrders = await GetAllAsync<ComponentOrder>("componentorders");

                foreach (var order in orders)
                {
                    // Восстанавливаем связи
                    order.Status = statuses.FirstOrDefault(s => s.Id == order.StatusId);
                    order.OrderType = orderTypes.FirstOrDefault(ot => ot.Id == order.OrderTypeId);
                    order.User = users.FirstOrDefault(u => u.Id == order.UserId);

                    // Восстанавливаем ComputerOrders
                    var coList = computerOrders.Where(co => co.OrderId == order.Id).ToList();
                    order.ComputerOrders = new HashSet<ComputerOrder>(coList);

                    // Восстанавливаем ComponentOrders
                    var compoList = componentOrders.Where(co => co.OrderId == order.Id).ToList();
                    order.ComponentOrders = new HashSet<ComponentOrder>(compoList);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при восстановлении связей заказов");
            }
        }

        private async Task TryRepairJsonFileAsync(string filePath, string json)
        {
            try
            {
                _logger.LogWarning("Пытаюсь восстановить поврежденный JSON файл: {FilePath}", filePath);

                var repairedJson = json;

                if (json.StartsWith("\uFEFF", StringComparison.Ordinal))
                {
                    repairedJson = json.Substring(1);
                    _logger.LogDebug("Удален BOM из файла");
                }

                var openBraces = repairedJson.Count(c => c == '[' || c == '{');
                var closeBraces = repairedJson.Count(c => c == ']' || c == '}');

                if (openBraces > closeBraces)
                {
                    repairedJson += new string('}', openBraces - closeBraces);
                    _logger.LogDebug("Добавлены закрывающие скобки: {Count}", openBraces - closeBraces);
                }

                for (int i = repairedJson.Length - 1; i >= 0; i--)
                {
                    var testJson = repairedJson.Substring(0, i + 1);
                    if (IsValidJson(testJson))
                    {
                        var open = testJson.Count(c => c == '[' || c == '{');
                        var close = testJson.Count(c => c == ']' || c == '}');

                        if (open > close)
                        {
                            testJson += new string('}', open - close);
                        }

                        testJson += "]";

                        await File.WriteAllTextAsync(filePath, testJson);
                        _logger.LogInformation("Файл восстановлен: {FilePath}", filePath);
                        return;
                    }
                }

                _logger.LogWarning("Не удалось восстановить файл: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при восстановлении файла: {FilePath}", filePath);
            }
        }

        private bool IsValidJson(string json)
        {
            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<T?> GetByIdAsync<T>(string entityName, int id) where T : class
        {
            var items = await GetAllAsync<T>(entityName);
            var property = typeof(T).GetProperty("Id");

            if (property == null)
            {
                _logger.LogWarning("У типа {TypeName} нет свойства Id", typeof(T).Name);
                return null;
            }

            var result = items.FirstOrDefault(item =>
            {
                var value = property.GetValue(item);
                return value != null && (int)value == id;
            });

            if (result == null)
            {
                _logger.LogDebug("Запись с ID {Id} не найдена в {EntityName}", id, entityName);
            }

            return result;
        }

        public async Task<T> CreateAsync<T>(string entityName, T item)
        {

            try
            {
                var items = await GetAllAsync<T>(entityName);

                // Используем рефлексию для получения ID
                var idProperty = typeof(T).GetProperty("Id");
                if (idProperty != null)
                {
                    // Определяем тип ID
                    var idType = idProperty.PropertyType;

                    // Получаем максимальный ID
                    object maxId = items.Any()
                        ? items.Max(x => (int)Convert.ChangeType(idProperty.GetValue(x), typeof(int)))
                        : 0;

                    // Устанавливаем новый ID
                    var newId = Convert.ChangeType((int)maxId + 1, idType);
                    idProperty.SetValue(item, newId);

                    _logger.LogDebug("Создана запись {EntityName} с ID {Id}", entityName, newId);
                }

                items.Add(item);
                await SaveAllAsync(entityName, items);

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании записи {EntityName}", entityName);
                throw;
            }
        }

        public async Task<bool> UpdateAsync<T>(string entityName, T updatedItem) where T : class
        {

            try
            {
                var items = await GetAllAsync<T>(entityName);
                var idProperty = typeof(T).GetProperty("Id");

                if (idProperty == null)
                {
                    _logger.LogError("У типа {TypeName} нет свойства Id", typeof(T).Name);
                    return false;
                }

                var itemId = (int)(idProperty.GetValue(updatedItem) ?? 0);
                var index = items.FindIndex(item =>
                    (int)(idProperty.GetValue(item) ?? 0) == itemId);

                if (index == -1)
                {
                    _logger.LogWarning("Запись с ID {Id} не найдена в {EntityName}", itemId, entityName);
                    return false;
                }

                items[index] = updatedItem;
                await SaveAllAsync(entityName, items);

                _logger.LogDebug("Запись {EntityName} с ID {Id} обновлена", entityName, itemId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении записи {EntityName}", entityName);
                return false;
            }
        }

        public async Task<bool> DeleteAsync<T>(string entityName, int id) where T : class
        {

            try
            {
                var items = await GetAllAsync<T>(entityName);
                var property = typeof(T).GetProperty("Id");

                if (property == null)
                {
                    _logger.LogError("У типа {TypeName} нет свойства Id", typeof(T).Name);
                    return false;
                }

                var item = items.FirstOrDefault(x =>
                    (int)(property.GetValue(x) ?? 0) == id);

                if (item == null)
                {
                    _logger.LogWarning("Запись с ID {Id} не найдена в {EntityName}", id, entityName);
                    return false;
                }

                items.Remove(item);
                await SaveAllAsync(entityName, items);

                _logger.LogDebug("Запись {EntityName} с ID {Id} удалена", entityName, id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении записи {EntityName} с ID {Id}", entityName, id);
                return false;
            }
        }

        public async Task SaveAllAsync<T>(string entityName, List<T> items)
        {
            var filePath = GetFilePath(entityName, true);

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var json = JsonSerializer.Serialize(items, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);

                // Обновляем кэш содержимого
                var cacheKey = $"content_{NormalizeEntityName(entityName)}";
                _fileContentCache[cacheKey] = items;

                // Обновляем список существующих файлов
                RefreshExistingFiles(true);

                _logger.LogDebug("Сохранено {Count} записей в {EntityName}", items.Count, entityName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении {EntityName} в JSON", entityName);
                throw;
            }
        }

        public async Task<T?> FindAsync<T>(string entityName, Func<T, bool> predicate) where T : class
        {
            var items = await GetAllAsync<T>(entityName);
            return items.FirstOrDefault(predicate);
        }

        public async Task<bool> AnyAsync<T>(string entityName, Func<T, bool> predicate)
        {
            var items = await GetAllAsync<T>(entityName);
            return items.Any(predicate);
        }

        public async Task<int> CountAsync<T>(string entityName, Func<T, bool>? predicate = null)
        {
            var items = await GetAllAsync<T>(entityName);
            return predicate == null ? items.Count : items.Count(predicate);
        }

        // Новые методы для управления файлами

        public async Task<bool> FileExistsAsync(string entityName)
        {
            var filePath = GetFilePath(entityName);
            return File.Exists(filePath);
        }

        public List<string> GetExistingFileNames()
        {
            RefreshExistingFiles();
            return _existingFiles?.ToList() ?? new List<string>();
        }

        public async Task<bool> RenameFileAsync(string oldEntityName, string newEntityName)
        {
            var oldFilePath = GetFilePath(oldEntityName);
            var newFilePath = GetFilePath(newEntityName, true);

            if (!File.Exists(oldFilePath))
            {
                _logger.LogWarning("Исходный файл не найден: {OldFilePath}", oldFilePath);
                return false;
            }

            if (File.Exists(newFilePath))
            {
                _logger.LogWarning("Целевой файл уже существует: {NewFilePath}", newFilePath);
                return false;
            }

            try
            {
                File.Move(oldFilePath, newFilePath);

                // Инвалидируем кэши
                InvalidateCache(oldEntityName);
                InvalidateCache(newEntityName);

                _logger.LogInformation("Файл переименован: {Old} -> {New}",
                    oldEntityName, newEntityName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переименовании файла {Old} -> {New}",
                    oldEntityName, newEntityName);
                return false;
            }
        }

        public async Task<bool> DeleteFileAsync(string entityName)
        {
            var filePath = GetFilePath(entityName);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Файл не найден: {FilePath}", filePath);
                return false;
            }

            try
            {
                File.Delete(filePath);

                // Инвалидируем кэши
                InvalidateCache(entityName);

                _logger.LogInformation("Файл удален: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении файла {EntityName}", entityName);
                return false;
            }
        }

        public async Task BackupFileAsync(string entityName, string backupSuffix = "_backup")
        {
            var filePath = GetFilePath(entityName);

            if (!File.Exists(filePath))
                return;

            var backupPath = filePath.Replace(".json", $"{backupSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.json");

            try
            {
                File.Copy(filePath, backupPath);
                _logger.LogInformation("Создана резервная копия: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании резервной копии файла {EntityName}", entityName);
            }
        }

        // Метод для диагностики
        public async Task<FileDiagnostics> GetFileDiagnostics(string entityName)
        {
            var filePath = GetFilePath(entityName);

            var diagnostics = new FileDiagnostics
            {
                EntityName = entityName,
                FilePath = filePath,
                Exists = File.Exists(filePath),
                NormalizedName = NormalizeEntityName(entityName)
            };

            if (diagnostics.Exists)
            {
                var fileInfo = new FileInfo(filePath);
                diagnostics.FileSize = fileInfo.Length;
                diagnostics.LastModified = fileInfo.LastWriteTime;

                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    diagnostics.IsValidJson = IsValidJson(json);

                    if (diagnostics.IsValidJson)
                    {
                        var items = await GetAllAsync<object>(entityName);
                        diagnostics.ItemCount = items.Count;
                    }
                }
                catch
                {
                    diagnostics.IsValidJson = false;
                }
            }

            return diagnostics;
        }
    }

    // Класс для диагностики файлов
    public class FileDiagnostics
    {
        public string EntityName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsValidJson { get; set; }
        public int ItemCount { get; set; }
    }
}