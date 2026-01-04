using System.Globalization;
using System.Text;
using System.Text.Json;
using WebShopV3.Models;

namespace WebShopV3.Services
{
    public interface IYookassaService
    {
        Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request);
        Task<PaymentResponse> GetPaymentStatusAsync(string paymentId);
    }

    public class YookassaService : IYookassaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _shopId;
        private readonly string _secretKey;
        private readonly string _baseUrl;

        public YookassaService(IConfiguration configuration, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _shopId = configuration["Yookassa:ShopId"];
            _secretKey = configuration["Yookassa:SecretKey"];
            _baseUrl = configuration["Yookassa:BaseUrl"] ?? "https://api.yookassa.ru/v3/";

            // Базовая аутентификация
            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_shopId}:{_secretKey}"));
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {authString}");
            _httpClient.DefaultRequestHeaders.Add("Idempotence-Key", Guid.NewGuid().ToString());
        }

        public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
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

            return new PaymentResponse
            {
                Id = root.GetProperty("id").GetString(),
                Status = root.GetProperty("status").GetString(),
                ConfirmationUrl = root.GetProperty("confirmation").GetProperty("confirmation_url").GetString(),
                Amount = decimal.Parse(
                    root.GetProperty("amount").GetProperty("value").GetString(),
                    CultureInfo.InvariantCulture) // Используем InvariantCulture здесь
            };
        }

        public async Task<PaymentResponse> GetPaymentStatusAsync(string paymentId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}payments/{paymentId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Ошибка получения статуса платежа: {responseContent}");
            }

            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            return new PaymentResponse
            {
                Id = root.GetProperty("id").GetString(),
                Status = root.GetProperty("status").GetString(),
                Paid = root.GetProperty("paid").GetBoolean(),
                Amount = decimal.Parse(root.GetProperty("amount").GetProperty("value").GetString())
            };
        }
    }
}
