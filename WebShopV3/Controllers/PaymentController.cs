using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShopV3.Models;
using WebShopV3.Services;

namespace WebShopV3.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IYookassaService _yookassaService;
        private readonly ApplicationDbContext _context;
        private const string CartSessionKey = "Cart";

        public PaymentController(IYookassaService yookassaService, ApplicationDbContext context)
        {
            _yookassaService = yookassaService;
            _context = context;
        }

        // GET: Payment/Process
        [HttpPost]
        public async Task<IActionResult> Process(int orderId, decimal amount)
        {
            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Заказ не найден";
                    return RedirectToAction("Checkout", "Cart");
                }

                // Проверяем, что заказ еще не оплачен
                if (order.StatusId == 4) // 1 - Завершен (оплачен)
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

                // Сохраняем ID платежа в заказе (добавьте поле PaymentId в модель Order)
                // order.PaymentId = paymentResponse.Id;
                // await _context.SaveChangesAsync();

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
                var order = await _context.Orders
                    .Include(o => o.ComputerOrders)
                    .Include(o => o.ComponentOrders)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Заказ не найден" });
                }

                // Списание товаров со склада
                foreach (var computerOrder in order.ComputerOrders)
                {
                    var computer = await _context.Computers.FindAsync(computerOrder.ComputerId);
                    if (computer != null)
                    {
                        computer.Quantity -= computerOrder.Quantity;
                        _context.Computers.Update(computer);
                    }
                }

                foreach (var componentOrder in order.ComponentOrders)
                {
                    var component = await _context.Components.FindAsync(componentOrder.ComponentId);
                    if (component != null)
                    {
                        component.Quantity -= componentOrder.Quantity;
                        _context.Components.Update(component);
                    }
                }

                // Обновляем статус заказа на "Оплачен"
                order.StatusId = 4; // Завершен
                _context.Orders.Update(order);

                await _context.SaveChangesAsync();

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
                // Загружаем заказ с связанными данными
                var order = await _context.Orders
                    .Include(o => o.ComputerOrders)
                    .ThenInclude(co => co.Computer)
                    .Include(o => o.ComponentOrders)
                    .ThenInclude(co => co.Component)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Заказ не найден";
                    return RedirectToAction("Index", "Home");
                }

                Console.WriteLine($"Обработка заказа #{orderId}, текущий статус: {order.StatusId}");

                // Проверяем статус заказа
                if (order.StatusId == 4) // Уже оплачен
                {
                    TempData["SuccessMessage"] = $"Заказ #{orderId} уже оплачен!";
                    return View(order);
                }

                if (order.StatusId == 6) // Отменен
                {
                    TempData["ErrorMessage"] = "Заказ был отменен";
                    return RedirectToAction("MyOrders", "Order");
                }

                // Списание товаров
                bool hasStockIssues = false;

                foreach (var computerOrder in order.ComputerOrders)
                {
                    var computer = computerOrder.Computer;
                    if (computer != null && computer.Quantity >= computerOrder.Quantity)
                    {
                        computer.Quantity -= computerOrder.Quantity;
                        Console.WriteLine($"Списан компьютер: {computer.Name}");
                    }
                    else
                    {
                        hasStockIssues = true;
                        Console.WriteLine($"Проблема с компьютером: {computer?.Name}");
                        break;
                    }
                }

                if (!hasStockIssues)
                {
                    foreach (var componentOrder in order.ComponentOrders)
                    {
                        var component = componentOrder.Component;
                        if (component != null && component.Quantity >= componentOrder.Quantity)
                        {
                            component.Quantity -= componentOrder.Quantity;
                            Console.WriteLine($"Списан компонент: {component.Name}");
                        }
                        else
                        {
                            hasStockIssues = true;
                            Console.WriteLine($"Проблема с компонентом: {component?.Name}");
                            break;
                        }
                    }
                }

                if (hasStockIssues)
                {
                    order.StatusId = 7; // Проблема с наличием
                    await _context.SaveChangesAsync();
                    TempData["ErrorMessage"] = "К сожалению, некоторых товаров нет в достаточном количестве. Мы свяжемся с вами для уточнения деталей.";
                    return RedirectToAction("MyOrders", "Order");
                }

                // Меняем статус на "Оплачен"
                order.StatusId = 4;
                await _context.SaveChangesAsync();

                // Очищаем корзину
                HttpContext.Session.Remove("Cart");

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
        public IActionResult Failure(int orderId)
        {
            TempData["ErrorMessage"] = "Оплата не была завершена. Пожалуйста, попробуйте еще раз.";
            return RedirectToAction("Checkout", "Cart");
        }
    }
}
