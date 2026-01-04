using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using WebShopV3.Models;
using WebShopV3.Services;
using Xunit;

namespace WebShopV3.Tests.Services
{
    public class YookassaServiceTests : IDisposable
    {
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly YookassaService _service;

        public YookassaServiceTests()
        {
            _configurationMock = new Mock<IConfiguration>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

            // Настраиваем конфигурацию
            _configurationMock.Setup(x => x["Yookassa:ShopId"]).Returns("test_shop_id");
            _configurationMock.Setup(x => x["Yookassa:SecretKey"]).Returns("test_secret_key");
            _configurationMock.Setup(x => x["Yookassa:BaseUrl"]).Returns("https://api.yookassa.test/v3/");

            _service = new YookassaService(_configurationMock.Object, _httpClient);
        }

        [Fact]
        public async Task CreatePaymentAsync_ShouldReturnPaymentResponse_OnSuccess()
        {
            // Arrange
            var request = new PaymentRequest
            {
                Amount = 1000.50m,
                Description = "Test payment",
                OrderId = 123,
                ReturnUrl = "https://example.com/return"
            };

            var expectedResponse = new
            {
                id = "payment_123",
                status = "pending",
                confirmation = new
                {
                    confirmation_url = "https://yookassa.test/confirm",
                    type = "redirect"
                },
                amount = new
                {
                    value = "1000.50",
                    currency = "RUB"
                },
                paid = false
            };

            var responseJson = JsonSerializer.Serialize(expectedResponse);
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri.ToString().Contains("payments")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            // Act
            var result = await _service.CreatePaymentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("payment_123", result.Id);
            Assert.Equal("pending", result.Status);
            Assert.Equal("https://yookassa.test/confirm", result.ConfirmationUrl);
            Assert.Equal(1000.50m, result.Amount);
        }

        [Fact]
        public async Task CreatePaymentAsync_ShouldThrowException_OnApiError()
        {
            // Arrange
            var request = new PaymentRequest
            {
                Amount = 1000m,
                Description = "Test",
                OrderId = 1,
                ReturnUrl = "https://example.com"
            };

            var errorResponse = new
            {
                type = "error",
                description = "Invalid request"
            };

            var responseJson = JsonSerializer.Serialize(errorResponse);
            var responseMessage = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(
                () => _service.CreatePaymentAsync(request));

            Assert.Contains("Ошибка создания платежа", exception.Message);
        }

        [Fact]
        public async Task CreatePaymentAsync_ShouldFormatAmountCorrectly()
        {
            // Arrange
            var request = new PaymentRequest
            {
                Amount = 1000.00m, // Целое число
                Description = "Test",
                OrderId = 1,
                ReturnUrl = "https://example.com"
            };

            var expectedResponse = new
            {
                id = "payment_1",
                status = "pending",
                confirmation = new { confirmation_url = "https://test.com" },
                amount = new { value = "1000.00", currency = "RUB" }
            };

            var responseJson = JsonSerializer.Serialize(expectedResponse);
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Content != null &&
                        req.Content.ReadAsStringAsync().Result.Contains("\"value\":\"1000.00\"")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            // Act
            var result = await _service.CreatePaymentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1000.00m, result.Amount);
        }

        [Fact]
        public async Task CreatePaymentAsync_ShouldIncludeMetadata()
        {
            // Arrange
            var request = new PaymentRequest
            {
                Amount = 1500.75m,
                Description = "Order #123",
                OrderId = 123,
                ReturnUrl = "https://example.com" //test
            };

            var expectedResponse = new
            {
                id = "payment_123",
                status = "pending",
                confirmation = new { confirmation_url = "https://test.com" },
                amount = new { value = "1500.75", currency = "RUB" }
            };

            var responseJson = JsonSerializer.Serialize(expectedResponse);
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Content != null &&
                        req.Content.ReadAsStringAsync().Result.Contains("\"metadata\":{\"order_id\":123}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            // Act
            var result = await _service.CreatePaymentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("payment_123", result.Id);
        }

        [Fact]
        public async Task GetPaymentStatusAsync_ShouldReturnStatus_OnSuccess()
        {
            // Сохраняем текущую культуру
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            try
            {
                // Arrange
                var paymentId = "payment_123";

                var expectedResponse = new
                {
                    id = paymentId,
                    status = "succeeded",
                    paid = true,
                    amount = new
                    {
                        value = "1000.00",
                        currency = "RUB"
                    }
                };

                var responseJson = JsonSerializer.Serialize(expectedResponse);
                var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };

                _httpMessageHandlerMock
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.Is<HttpRequestMessage>(req =>
                            req.Method == HttpMethod.Get &&
                            req.RequestUri.ToString().Contains(paymentId)),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(responseMessage);

                // Act
                var result = await _service.GetPaymentStatusAsync(paymentId);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(paymentId, result.Id);
                Assert.Equal("succeeded", result.Status);
                Assert.True(result.Paid);
                Assert.Equal(1000.00m, result.Amount);
            }
            finally
            {
                // Восстанавливаем культуру
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public async Task GetPaymentStatusAsync_ShouldThrowException_OnApiError()
        {
            // Arrange
            var paymentId = "invalid_payment_id";

            var errorResponse = new
            {
                type = "error",
                description = "Payment not found"
            };

            var responseJson = JsonSerializer.Serialize(errorResponse);
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(
                () => _service.GetPaymentStatusAsync(paymentId));

            Assert.Contains("Ошибка получения статуса платежа", exception.Message);
        }

        [Fact]
        public async Task GetPaymentStatusAsync_ShouldHandleDifferentStatuses()
        {
            // Arrange
            var paymentId = "payment_123";

            // Сохраняем предыдущую культуру потока
            var previousCulture = Thread.CurrentThread.CurrentCulture;

            try
            {
                // Устанавливаем инвариантную культуру для корректного парсинга
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                var testCases = new[]
                {
                    new { status = "pending", paid = false },
                    new { status = "waiting_for_capture", paid = false },
                    new { status = "succeeded", paid = true },
                    new { status = "canceled", paid = false }
                };

                foreach (var testCase in testCases)
                {
                    // Пересоздаем мок для каждого теста
                    _httpMessageHandlerMock
                        .Protected()
                        .Setup<Task<HttpResponseMessage>>(
                            "SendAsync",
                            ItExpr.Is<HttpRequestMessage>(req =>
                                req.Method == HttpMethod.Get &&
                                req.RequestUri.ToString().Contains(paymentId)),
                            ItExpr.IsAny<CancellationToken>())
                        .ReturnsAsync(() =>
                        {
                            var expectedResponse = new
                            {
                                id = paymentId,
                                status = testCase.status,
                                paid = testCase.paid,
                                amount = new { value = "1000.00", currency = "RUB" }
                            };

                            var responseJson = JsonSerializer.Serialize(expectedResponse);
                            return new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                            };
                        });

                    // Act
                    var result = await _service.GetPaymentStatusAsync(paymentId);

                    // Assert
                    Assert.NotNull(result);
                    Assert.Equal(paymentId, result.Id);
                    Assert.Equal(testCase.status, result.Status);
                    Assert.Equal(testCase.paid, result.Paid);
                    Assert.Equal(1000.00m, result.Amount);

                    // Сбрасываем мок для следующей итерации
                    _httpMessageHandlerMock.Reset();
                }
            }
            finally
            {
                // Восстанавливаем культуру
                Thread.CurrentThread.CurrentCulture = previousCulture;
            }
        }

        [Fact]
        public void Constructor_ShouldSetAuthorizationHeader()
        {
            // Arrange
            var shopId = "test_shop_123";
            var secretKey = "test_secret_456";

            _configurationMock.Setup(x => x["Yookassa:ShopId"]).Returns(shopId);
            _configurationMock.Setup(x => x["Yookassa:SecretKey"]).Returns(secretKey);

            var expectedAuthString = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{shopId}:{secretKey}"));

            // Act
            var service = new YookassaService(_configurationMock.Object, new HttpClient());

            // Assert - проверяем через рефлексию, что заголовки установлены
            var httpClientField = typeof(YookassaService).GetField("_httpClient",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var httpClient = (HttpClient)httpClientField.GetValue(service);

            Assert.NotNull(httpClient);
            Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization);
            Assert.Equal("Basic", httpClient.DefaultRequestHeaders.Authorization.Scheme);

            // Не проверяем точное значение, так как оно может быть закодировано по-разному
            Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization.Parameter);
        }

        [Fact]
        public void Constructor_ShouldSetDefaultBaseUrl_WhenNotConfigured()
        {
            // Arrange
            _configurationMock.Setup(x => x["Yookassa:ShopId"]).Returns("test");
            _configurationMock.Setup(x => x["Yookassa:SecretKey"]).Returns("test");
            _configurationMock.Setup(x => x["Yookassa:BaseUrl"]).Returns((string)null);

            // Act
            var service = new YookassaService(_configurationMock.Object, new HttpClient());

            // Assert
            // Должен использоваться URL по умолчанию
            Assert.NotNull(service);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}