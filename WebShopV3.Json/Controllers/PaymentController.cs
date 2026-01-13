using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;
using Order = WebShopV3.Json.Models.Order;
using PaymentRequest = WebShopV3.Json.Models.PaymentRequest;

namespace WebShopV3.Json.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IYookassaService _yookassaService;
        private readonly JsonDataService _jsonData;
        private const string CartSessionKey = "Cart";

        public PaymentController(IYookassaService yookassaService, JsonDataService jsonData)
        {
            _yookassaService = yookassaService;
            _jsonData = jsonData;
        }

        // POST: Payment/Process
        [HttpPost]
        public async Task<IActionResult> Process(int orderId, decimal amount)
        {
            try
            {
                var order = await _jsonData.GetByIdAsync<Order>("Orders", orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Заказ не найден";
                    return RedirectToAction("Checkout", "Cart");
                }

                // Проверяем, что заказ еще не оплачен
                if (order.StatusId == 4) // Завершен (оплачен)
                {
                    TempData["ErrorMessage"] = "Заказ уже оплачен";
                    return RedirectToAction("MyOrders", "Order");
                }

                var paymentRequest = new PaymentRequest
                {
                    Amount = amount,
                    Description = $"Оплата заказа #{orderId}",
                    OrderId = orderId,
                    ReturnUrl = Url.Action("Success", "Payment", new { orderId }, Request.Scheme) ??
                               $"{Request.Scheme}://{Request.Host}/Payment/Success?orderId={orderId}"
                };

                var paymentResponse = await _yookassaService.CreatePaymentAsync(paymentRequest);

                // Сохраняем ID платежа в заказе (если нужно)
                // order.PaymentId = paymentResponse.Id;
                // await _jsonData.UpdateAsync("Orders", order);

                return Redirect(paymentResponse.ConfirmationUrl);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при создании платежа: {ex.Message}";
                return RedirectToAction("Checkout", "Cart");
            }
        }

        // POST: Payment/ConfirmPayment
        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(int orderId)
        {
            try
            {
                var order = await GetOrderWithDetails(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Заказ не найден" });
                }

                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var components = await _jsonData.GetAllAsync<Component>("Components");

                // Списание товаров со склада
                foreach (var computerOrder in order.ComputerOrders)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == computerOrder.ComputerId);
                    if (computer != null)
                    {
                        computer.Quantity -= computerOrder.Quantity;
                        await _jsonData.UpdateAsync("Computers", computer);
                    }
                }

                foreach (var componentOrder in order.ComponentOrders)
                {
                    var component = components.FirstOrDefault(c => c.Id == componentOrder.ComponentId);
                    if (component != null)
                    {
                        component.Quantity -= componentOrder.Quantity;
                        await _jsonData.UpdateAsync("Components", component);
                    }
                }

                // Обновляем статус заказа на "Оплачен"
                order.StatusId = 4; // Завершен
                await _jsonData.UpdateAsync("Orders", order);

                // Очищаем корзину
                HttpContext.Session.Remove("Cart");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Success(int orderId)
        {
            try
            {
                var order = await GetOrderWithDetails(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Заказ не найден";
                    return RedirectToAction("Index", "Home");
                }

                Console.WriteLine($"Обработка заказа #{orderId}, текущий статус: {order.StatusId}");

                // Проверяем статус заказа
                if (order.StatusId == 4) // Уже завершен (оплачен)
                {
                    TempData["SuccessMessage"] = $"Заказ #{orderId} уже оплачен!";
                    return View(order);
                }

                if (order.StatusId == 5) // Отменен
                {
                    TempData["ErrorMessage"] = "Заказ был отменен";
                    return RedirectToAction("MyOrders", "Order");
                }

                // Проверяем, что заказ находится в статусе "В ожидании" (ожидает оплаты)
                if (order.StatusId == 2) // В ожидании
                {
                    // Только меняем статус на "Завершен"
                    order.StatusId = 4;
                    Console.WriteLine($"Статус заказа #{orderId} изменен на 'Завершен'");
                }
                else
                {
                    Console.WriteLine($"Заказ #{orderId} имеет неожиданный статус: {order.StatusId}");
                }

                await _jsonData.UpdateAsync("Orders", order);

                // Очищаем корзину
                HttpContext.Session.Remove(CartSessionKey);

                TempData["SuccessMessage"] = $"Заказ #{orderId} успешно оплачен! Товары будут отправлены в ближайшее время.";
                return View(order);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                TempData["ErrorMessage"] = $"Ошибка при обработке заказа: {ex.Message}";
                return RedirectToAction("MyOrders", "Order");
            }
        }

        // GET: Payment/Failure
        [HttpGet]
        public IActionResult Failure(int orderId)
        {
            TempData["ErrorMessage"] = "Оплата не была завершена. Пожалуйста, попробуйте еще раз.";
            return RedirectToAction("Checkout", "Cart");
        }

        // Вспомогательные методы

        private async Task<Order?> GetOrderWithDetails(int orderId)
        {
            var order = await _jsonData.GetByIdAsync<Order>("Orders", orderId);
            if (order == null) return null;

            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");
            var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
            var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");

            // Загружаем ComputerOrders
            var coList = computerOrders.Where(co => co.OrderId == orderId).ToList();
            foreach (var co in coList)
            {
                co.Computer = computers.FirstOrDefault(c => c.Id == co.ComputerId);
            }
            order.ComputerOrders = coList;

            // Загружаем ComponentOrders
            var compoList = componentOrders.Where(co => co.OrderId == orderId).ToList();
            foreach (var co in compoList)
            {
                co.Component = components.FirstOrDefault(c => c.Id == co.ComponentId);
            }
            order.ComponentOrders = compoList;

            return order;
        }

        // Дополнительный метод для обработки вебхуков от ЮКассы (если нужно)
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                // Читаем тело запроса
                using var reader = new StreamReader(Request.Body);
                var requestBody = await reader.ReadToEndAsync();

                // Здесь должна быть логика проверки подписи от ЮКассы
                // и обработки разных событий (payment.succeeded, payment.canceled и т.д.)

                // Пример обработки успешного платежа
                var paymentData = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);
                if (paymentData != null &&
                    paymentData.ContainsKey("event") &&
                    paymentData["event"]?.ToString() == "payment.succeeded")
                {
                    var objectData = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        paymentData["object"]?.ToString() ?? "{}");

                    if (objectData != null && objectData.ContainsKey("metadata"))
                    {
                        var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            objectData["metadata"]?.ToString() ?? "{}");

                        if (metadata != null && metadata.ContainsKey("orderId") &&
                            int.TryParse(metadata["orderId"]?.ToString(), out int orderId))
                        {
                            // Находим заказ и обновляем статус
                            var order = await _jsonData.GetByIdAsync<Order>("Orders", orderId);
                            if (order != null && order.StatusId != 4) // Если еще не завершен
                            {
                                order.StatusId = 4; // Завершен
                                await _jsonData.UpdateAsync("Orders", order);
                            }
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в вебхуке: {ex.Message}");
                return StatusCode(500);
            }
        }
    }
}