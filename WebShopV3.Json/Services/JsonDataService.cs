// Services/JsonDataService.cs
using System.Text.Json;

namespace WebShopV3.Json.Services
{
    public class JsonDataService
    {
        private readonly string _dataPath;
        private readonly ILogger<JsonDataService> _logger;
        public string DataPath => _dataPath;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public JsonDataService(ILogger<JsonDataService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _dataPath = Path.Combine(env.ContentRootPath, "Data", "Json");

            // Создаем директорию, если ее нет
            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }
        }

        private string GetFilePath(string entityName)
        {
            return Path.Combine(_dataPath, $"{entityName}.json");
        }

        public async Task<List<T>> GetAllAsync<T>(string entityName)
        {
            try
            {
                var filePath = GetFilePath(entityName);

                if (!File.Exists(filePath))
                {
                    return new List<T>();
                }

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении {EntityName} из JSON", entityName);
                return new List<T>();
            }
        }

        public async Task<T?> GetByIdAsync<T>(string entityName, int id) where T : class
        {
            var items = await GetAllAsync<T>(entityName);
            var property = typeof(T).GetProperty("Id");

            if (property == null)
                return null;

            return items.FirstOrDefault(item =>
                (int)(property.GetValue(item) ?? 0) == id);
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
            var items = await GetAllAsync<T>(entityName);
            var idProperty = typeof(T).GetProperty("Id");

            if (idProperty == null)
                return false;

            var itemId = (int)(idProperty.GetValue(updatedItem) ?? 0);
            var index = items.FindIndex(item =>
                (int)(idProperty.GetValue(item) ?? 0) == itemId);

            if (index == -1)
                return false;

            items[index] = updatedItem;
            await SaveAllAsync(entityName, items);
            return true;
        }

        public async Task<bool> DeleteAsync<T>(string entityName, int id) where T : class
        {
            var items = await GetAllAsync<T>(entityName);
            var property = typeof(T).GetProperty("Id");

            if (property == null)
                return false;

            var item = items.FirstOrDefault(x =>
                (int)(property.GetValue(x) ?? 0) == id);

            if (item == null)
                return false;

            items.Remove(item);
            await SaveAllAsync(entityName, items);
            return true;
        }

        public async Task SaveAllAsync<T>(string entityName, List<T> items)
        {
            try
            {
                var filePath = GetFilePath(entityName);
                var json = JsonSerializer.Serialize(items, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
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
    }
}