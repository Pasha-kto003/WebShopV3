using System.Text.Json;

namespace WebShopV3.Json.Services
{
    public class JsonDataFixer
    {
        private readonly JsonDataService _jsonData;
        private readonly ILogger<JsonDataFixer> _logger;

        public JsonDataFixer(JsonDataService jsonData, ILogger<JsonDataFixer> logger)
        {
            _jsonData = jsonData;
            _logger = logger;
        }

        public async Task FixOrdersFileAsync()
        {
            try
            {
                _logger.LogInformation("Начинаю исправление файла заказов...");

                // 1. Прочитаем текущий файл как текст
                var filePath = Path.Combine(_jsonData.DataPath, "orders.json");

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Файл заказов не найден: {FilePath}", filePath);
                    return;
                }

                var json = await File.ReadAllTextAsync(filePath);

                // 2. Парсим и исправляем структуру
                using var document = JsonDocument.Parse(json);
                var fixedOrders = new List<Dictionary<string, object>>();

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var orderDict = new Dictionary<string, object>();

                    // Копируем все простые свойства
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (prop.Name.Equals("status", StringComparison.OrdinalIgnoreCase))
                        {
                            // Преобразуем Status в statusId
                            if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                if (prop.Value.TryGetProperty("id", out var statusId))
                                {
                                    orderDict["statusId"] = statusId.GetInt32();
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                orderDict["statusId"] = prop.Value.GetInt32();
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                // Пропускаем строковое значение - оно будет исправлено позже
                                continue;
                            }
                        }
                        else if (prop.Name.Equals("orderType", StringComparison.OrdinalIgnoreCase))
                        {
                            // Аналогично для OrderType
                            if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                if (prop.Value.TryGetProperty("id", out var orderTypeId))
                                {
                                    orderDict["orderTypeId"] = orderTypeId.GetInt32();
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                orderDict["orderTypeId"] = prop.Value.GetInt32();
                            }
                        }
                        else if (prop.Name.Equals("user", StringComparison.OrdinalIgnoreCase))
                        {
                            // Для User берем только ID
                            if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                if (prop.Value.TryGetProperty("id", out var userId))
                                {
                                    orderDict["userId"] = userId.GetInt32();
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                orderDict["userId"] = prop.Value.GetInt32();
                            }
                        }
                        else
                        {
                            // Копируем другие свойства как есть
                            orderDict[prop.Name] = GetValue(prop.Value);
                        }
                    }

                    // Добавляем отсутствующие обязательные поля
                    if (!orderDict.ContainsKey("statusId"))
                        orderDict["statusId"] = 1; // Значение по умолчанию

                    if (!orderDict.ContainsKey("orderTypeId"))
                        orderDict["orderTypeId"] = 1; // Значение по умолчанию

                    if (!orderDict.ContainsKey("userId"))
                        orderDict["userId"] = 1; // Значение по умолчанию

                    fixedOrders.Add(orderDict);
                }

                // 3. Сохраняем исправленный файл
                var fixedJson = JsonSerializer.Serialize(fixedOrders, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Создаем резервную копию
                var backupPath = filePath + ".backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(filePath, backupPath);

                // Сохраняем исправленный файл
                await File.WriteAllTextAsync(filePath, fixedJson);

                _logger.LogInformation("Файл заказов успешно исправлен. Резервная копия: {BackupPath}", backupPath);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при исправлении файла заказов");
                throw;
            }
        }

        private object GetValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => element.ToString()
            };
        }

        public async Task FixAllDataFilesAsync()
        {
            _logger.LogInformation("Начинаю исправление всех файлов данных...");

            await FixOrdersFileAsync();

            _logger.LogInformation("Исправление всех файлов данных завершено");
        }
    }
}
