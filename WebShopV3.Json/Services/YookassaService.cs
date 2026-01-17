using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WebShopV3.Json.Models;

namespace WebShopV3.Json.Services
{
    public interface IYookassaService
    {
        Task<PaymentResponse> CreatePaymentAsync(Models.PaymentRequest request);
        Task<PaymentResponse> GetPaymentStatusAsync(string paymentId);
        Task<PaymentResponse> CapturePaymentAsync(string paymentId);
        Task<PaymentResponse> CancelPaymentAsync(string paymentId);
        Task UpdateOrderPaymentStatusAsync(int orderId, string paymentId, string status);
    }

    public class YookassaService : IYookassaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _dataPath;
        private readonly string _shopId;
        private readonly string _secretKey;
        private readonly string _baseUrl;
        private readonly ILogger<YookassaService> _logger;
        private const string PAYMENTS_FILE = "payments.json";

        public YookassaService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<YookassaService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("Yookassa");
            _shopId = configuration["Yookassa:ShopId"] ?? throw new ArgumentNullException(nameof(_shopId));
            _secretKey = configuration["Yookassa:SecretKey"] ?? throw new ArgumentNullException(nameof(_secretKey));
            _baseUrl = configuration["Yookassa:BaseUrl"] ?? "https://api.yookassa.ru/v3/";
            _logger = logger;

            // Настройка базовой аутентификации
            var authString = $"{_shopId}:{_secretKey}";
            var authBytes = Encoding.UTF8.GetBytes(authString);
            var authBase64 = Convert.ToBase64String(authBytes);

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authBase64);

            // Добавляем заголовок Idempotence-Key
            _httpClient.DefaultRequestHeaders.Add("Idempotence-Key", Guid.NewGuid().ToString());

            // Настройка таймаутов
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<PaymentResponse> CreatePaymentAsync(Models.PaymentRequest request)
        {
            try
            {
                // Форматируем сумму с двумя знаками после запятой
                var amountValue = request.Amount.ToString("0.00", CultureInfo.InvariantCulture);

                var paymentData = new
                {
                    amount = new
                    {
                        value = amountValue,
                        currency = "RUB"
                    },
                    capture = true,
                    confirmation = new
                    {
                        type = "redirect",
                        return_url = request.ReturnUrl
                    },
                    description = request.Description,
                    metadata = new
                    {
                        order_id = request.OrderId
                    }
                };

                var json = JsonSerializer.Serialize(paymentData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}payments", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка создания платежа: {responseContent}");
                }

                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                var paymentResponse = new PaymentResponse
                {
                    Id = root.GetProperty("id").GetString(),
                    Status = root.GetProperty("status").GetString(),
                    ConfirmationUrl = root.GetProperty("confirmation").GetProperty("confirmation_url").GetString(),
                    Amount = decimal.Parse(
                        root.GetProperty("amount").GetProperty("value").GetString(),
                        CultureInfo.InvariantCulture)
                };

                // Сохраняем информацию о платеже в JSON
                await SavePaymentToJsonAsync(paymentResponse);

                _logger.LogInformation($"Создан платеж {paymentResponse.Id} для заказа {request.OrderId}");

                return paymentResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании платежа");
                throw;
            }
        }

        public async Task<PaymentResponse> CreatePaymentForOrderAsync(int orderId)
        {
            try
            {
                // Загружаем информацию о заказе из JSON
                var order = await GetOrderFromJsonAsync(orderId);
                if (order == null)
                    throw new Exception($"Заказ {orderId} не найден");

                var request = new Models.PaymentRequest
                {
                    OrderId = orderId,
                    Amount = order.TotalAmount,
                    Description = $"Оплата заказа #{orderId}",
                    ReturnUrl = $"/orders/{orderId}/payment-success"
                };

                return await CreatePaymentAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании платежа для заказа {orderId}");
                throw;
            }
        }

        public async Task<PaymentResponse> GetPaymentStatusAsync(string paymentId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}payments/{paymentId}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка получения статуса платежа: {responseContent}");
                }

                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                var paymentResponse = new PaymentResponse
                {
                    Id = root.GetProperty("id").GetString(),
                    Status = root.GetProperty("status").GetString(),
                    Paid = root.TryGetProperty("paid", out var paidElement) ? paidElement.GetBoolean() : false,
                    Amount = decimal.Parse(
                        root.GetProperty("amount").GetProperty("value").GetString(),
                        CultureInfo.InvariantCulture)
                };

                // Обновляем статус в JSON
                await UpdatePaymentStatusInJsonAsync(paymentId, paymentResponse.Status, paymentResponse.Paid);

                return paymentResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении статуса платежа {paymentId}");

                // Пытаемся получить из локального хранилища
                var localPayment = await GetPaymentFromJsonAsync(paymentId);
                if (localPayment != null)
                    return localPayment;

                throw;
            }
        }

        public async Task<PaymentResponse> CapturePaymentAsync(string paymentId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}payments/{paymentId}/capture", null);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка подтверждения платежа: {responseContent}");
                }

                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                var paymentResponse = new PaymentResponse
                {
                    Id = root.GetProperty("id").GetString(),
                    Status = root.GetProperty("status").GetString(),
                    Paid = root.GetProperty("paid").GetBoolean(),
                    Amount = decimal.Parse(
                        root.GetProperty("amount").GetProperty("value").GetString(),
                        CultureInfo.InvariantCulture)
                };

                // Обновляем статус в JSON
                await UpdatePaymentStatusInJsonAsync(paymentId, paymentResponse.Status, paymentResponse.Paid);

                return paymentResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при подтверждении платежа {paymentId}");
                throw;
            }
        }

        public async Task<PaymentResponse> CancelPaymentAsync(string paymentId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}payments/{paymentId}/cancel", null);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка отмены платежа: {responseContent}");
                }

                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                var paymentResponse = new PaymentResponse
                {
                    Id = root.GetProperty("id").GetString(),
                    Status = root.GetProperty("status").GetString(),
                    Amount = decimal.Parse(
                        root.GetProperty("amount").GetProperty("value").GetString(),
                        CultureInfo.InvariantCulture)
                };

                // Обновляем статус в JSON
                await UpdatePaymentStatusInJsonAsync(paymentId, paymentResponse.Status, false);

                return paymentResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при отмене платежа {paymentId}");
                throw;
            }
        }

   

        public async Task UpdateOrderPaymentStatusAsync(int orderId, string paymentId, string status)
        {
            try
            {
                var orders = await LoadOrdersFromJsonAsync();
                var order = orders.FirstOrDefault(o => o.Id == orderId);

                if (order != null)
                {

                    if (status == "succeeded")
                    {
                        order.Status.Name = "Оплачен";
                    }
                    else if (status == "canceled")
                    {
                        order.Status.Name = "Отменен";
                    }

                    await SaveOrdersToJsonAsync(orders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обновлении статуса оплаты заказа {orderId}");
            }
        }

        // Методы для работы с JSON файлами
        private async Task<Order?> GetOrderFromJsonAsync(int orderId)
        {
            try
            {
                var orders = await LoadOrdersFromJsonAsync();
                return orders.FirstOrDefault(o => o.Id == orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке заказа {orderId} из JSON");
                return null;
            }
        }

        private async Task<List<Order>> LoadOrdersFromJsonAsync()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "orders.json");

                if (!File.Exists(filePath))
                    return new List<Order>();

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<Order>>(json) ?? new List<Order>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке заказов из JSON");
                return new List<Order>();
            }
        }

        private async Task SaveOrdersToJsonAsync(List<Order> orders)
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "orders.json");
                var directory = Path.GetDirectoryName(filePath);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory!);

                var json = JsonSerializer.Serialize(orders, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении заказов в JSON");
                throw;
            }
        }

        private async Task SavePaymentToJsonAsync(PaymentResponse payment)
        {
            try
            {
                var payments = await LoadPaymentsFromJsonAsync();
                payment.Paid = true;
                payments.Add(payment);

                var filePath = Path.Combine(_dataPath, PAYMENTS_FILE);
                var directory = Path.GetDirectoryName(filePath);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory!);

                var json = JsonSerializer.Serialize(payments, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(filePath, json);

                _logger.LogDebug($"Сохранен платеж {payment.Id} в JSON");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при сохранении платежа {payment.Id} в JSON");
            }
        }

        private async Task<List<PaymentResponse>> LoadPaymentsFromJsonAsync()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, PAYMENTS_FILE);

                if (!File.Exists(filePath))
                    return new List<PaymentResponse>();

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<PaymentResponse>>(json) ?? new List<PaymentResponse>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке платежей из JSON");
                return new List<PaymentResponse>();
            }
        }

        private async Task<PaymentResponse?> GetPaymentFromJsonAsync(string paymentId)
        {
            try
            {
                var payments = await LoadPaymentsFromJsonAsync();
                return payments.FirstOrDefault(p => p.Id == paymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении платежа {paymentId} из JSON");
                return null;
            }
        }

        private async Task UpdatePaymentStatusInJsonAsync(string paymentId, string status, bool? paid = null)
        {
            try
            {
                var payments = await LoadPaymentsFromJsonAsync();
                var payment = payments.FirstOrDefault(p => p.Id == paymentId);

                if (payment != null)
                {
                    payment.Status = status;
                    if (paid.HasValue)
                        payment.Paid = paid.Value;

                    var filePath = Path.Combine(_dataPath, PAYMENTS_FILE);
                    var json = JsonSerializer.Serialize(payments, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await File.WriteAllTextAsync(filePath, json);

                    _logger.LogDebug($"Обновлен статус платежа {paymentId} в JSON: {status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обновлении статуса платежа {paymentId} в JSON");
            }
        }
    }
}