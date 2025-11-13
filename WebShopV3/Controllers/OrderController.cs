using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebShopV3.Models;

namespace WebShopV3.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Order
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderType)
                .Include(o => o.Status)
                .Include(o => o.ComputerOrders)
                .ThenInclude(co => co.Computer)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: Order/MyOrders - Личные заказы пользователя
        public async Task<IActionResult> MyOrders()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderType)
                .Include(o => o.Status)
                .Include(o => o.ComputerOrders)
                .ThenInclude(co => co.Computer)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: Order/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderType)
                .Include(o => o.Status)
                .Include(o => o.ComputerOrders)
                .ThenInclude(co => co.Computer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // Проверка прав доступа: пользователь может видеть только свои заказы, админ/менеджер - все
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var userRole = User.FindFirst(ClaimTypes.Role).Value;

            if (userRole != "Админ" && userRole != "Менеджер" && order.UserId != userId)
            {
                return RedirectToAction("AccessDenied", "Auth");
            }

            return View(order);
        }

        // GET: Order/Create
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Create()
        {
            await LoadViewData();
            return View();
        }

        // POST: Order/Create
        // POST: Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Create(Order order, int[] selectedComputers, int[] quantities)
        {
            // ОТЛАДКА: выводим входящие данные
            Console.WriteLine("=== CREATE ORDER DEBUG ===");
            Console.WriteLine($"OrderTypeId: {order.OrderTypeId}");
            Console.WriteLine($"UserId: {order.UserId}");
            Console.WriteLine($"StatusId: {order.StatusId}");

            if (selectedComputers != null)
            {
                Console.WriteLine($"SelectedComputers count: {selectedComputers.Length}");
                for (int i = 0; i < selectedComputers.Length; i++)
                {
                    Console.WriteLine($"  Computer {i}: {selectedComputers[i]}");
                }
            }

            if (quantities != null)
            {
                Console.WriteLine($"Quantities count: {quantities.Length}");
                for (int i = 0; i < quantities.Length; i++)
                {
                    Console.WriteLine($"  Quantity {i}: {quantities[i]}");
                }
            }

            // Проверяем наличие данных
            if (selectedComputers == null || quantities == null)
            {
                TempData["ErrorMessage"] = "Ошибка в данных заказа: несоответствие выбранных компьютеров и количеств";
                await LoadViewData();
                return View(order);
            }

            // Фильтруем компьютеры с количеством > 0
            var validComputers = new List<(int ComputerId, int Quantity)>();
            for (int i = 0; i < selectedComputers.Length; i++)
            {
                if (quantities[i] > 0)
                {
                    validComputers.Add((selectedComputers[i], quantities[i]));
                    Console.WriteLine($"Valid computer: ID={selectedComputers[i]}, Qty={quantities[i]}");
                }
            }

            // Проверяем, есть ли валидные компьютеры
            if (!validComputers.Any())
            {
                TempData["ErrorMessage"] = "Не выбрано ни одного компьютера с количеством больше 0";
                await LoadViewData();
                return View(order);
            }

            // Получаем тип заказа
            var orderType = await _context.OrderTypes.FindAsync(order.OrderTypeId);
            bool isIncomeOrder = orderType?.Name?.ToLower() == "приход";

            Console.WriteLine($"Order type: {orderType?.Name}, Is income: {isIncomeOrder}");

            // Для заказов типа "Продажа" проверяем наличие товаров на складе
            if (!isIncomeOrder)
            {
                var stockErrors = new List<string>();
                foreach (var (computerId, quantity) in validComputers)
                {
                    var computer = await _context.Computers.FindAsync(computerId);
                    if (computer == null)
                    {
                        stockErrors.Add($"Компьютер с ID {computerId} не найден");
                    }
                    else if (computer.Quantity < quantity)
                    {
                        stockErrors.Add($"Недостаточно товара '{computer.Name}' в наличии. Доступно: {computer.Quantity}, Заказано: {quantity}");
                    }
                }

                if (stockErrors.Any())
                {
                    TempData["ErrorMessage"] = string.Join("<br>", stockErrors);
                    await LoadViewData();
                    return View(order);
                }
            }

            try
            {
                // Базовая настройка заказа
                order.OrderDate = DateTime.Now;
                order.TotalAmount = 0;

                // Сохраняем заказ чтобы получить ID
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Order created with ID: {order.Id}");

                decimal totalAmount = 0;

                // Обрабатываем компьютеры
                foreach (var (computerId, quantity) in validComputers)
                {
                    var computer = await _context.Computers.FindAsync(computerId);
                    if (computer != null)
                    {
                        var computerOrder = new ComputerOrder
                        {
                            OrderId = order.Id,
                            ComputerId = computerId,
                            Quantity = quantity,
                            UnitPrice = computer.Price
                        };

                        var itemTotal = computer.Price * quantity;
                        totalAmount += itemTotal;

                        Console.WriteLine($"Added: {computer.Name}, Qty: {quantity}, Price: {computer.Price}, Total: {itemTotal}");

                        // Обновляем склад в зависимости от типа заказа
                        if (isIncomeOrder)
                        {
                            // приход - увеличиваем количество
                            computer.Quantity += quantity;
                            Console.WriteLine($"Income order: increasing stock for {computer.Name} by {quantity}");
                        }
                        else
                        {
                            // Продажа - уменьшаем количество
                            computer.Quantity -= quantity;
                            Console.WriteLine($"Sale order: decreasing stock for {computer.Name} by {quantity}");
                        }

                        _context.Computers.Update(computer);
                        _context.ComputerOrders.Add(computerOrder);
                    }
                }

                // Обновляем сумму заказа
                order.TotalAmount = totalAmount;
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Final order total: {order.TotalAmount}");

                var orderTypeName = isIncomeOrder ? "приход" : "продажа";
                TempData["SuccessMessage"] = $"Заказ #{order.Id} ({orderTypeName}) успешно создан! Сумма: {order.TotalAmount:C}";

                return RedirectToAction("Details", new { id = order.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating order: {ex.Message}");
                TempData["ErrorMessage"] = $"Ошибка при создании заказа: {ex.Message}";
                await LoadViewData();
                return View(order);
            }
        }


        // GET: Order/Edit/5
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.ComputerOrders)
                .ThenInclude(co => co.Computer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            await LoadViewData();
            return View(order);
        }

        // POST: Order/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Edit(int id, Order order)
        {
            if (id != order.Id)
            {
                return NotFound();
            }

            try
            {
                var existingOrder = await _context.Orders
                    .Include(o => o.ComputerOrders)
                    .ThenInclude(co => co.Computer)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (existingOrder == null)
                {
                    return NotFound();
                }

                // Получаем старый и новый тип заказа
                var oldOrderType = await _context.OrderTypes.FindAsync(existingOrder.OrderTypeId);
                var newOrderType = await _context.OrderTypes.FindAsync(order.OrderTypeId);

                bool wasIncomeOrder = oldOrderType?.Name?.ToLower() == "приход";
                bool isIncomeOrder = newOrderType?.Name?.ToLower() == "приход";

                // Если тип заказа изменился, нужно скорректировать склад
                if (wasIncomeOrder != isIncomeOrder)
                {
                    foreach (var computerOrder in existingOrder.ComputerOrders)
                    {
                        var computer = await _context.Computers.FindAsync(computerOrder.ComputerId);
                        if (computer != null)
                        {
                            if (wasIncomeOrder && !isIncomeOrder)
                            {
                                // Было "приход", стало "Продажа" - убираем двойное количество
                                computer.Quantity -= computerOrder.Quantity * 2;
                                Console.WriteLine($"Changing from income to sale: removing double quantity for {computer.Name}");
                            }
                            else if (!wasIncomeOrder && isIncomeOrder)
                            {
                                // Было "Продажа", стало "приход" - добавляем двойное количество
                                computer.Quantity += computerOrder.Quantity * 2;
                                Console.WriteLine($"Changing from sale to income: adding double quantity for {computer.Name}");
                            }
                            _context.Computers.Update(computer);
                        }
                    }
                }

                // Обновляем основные данные заказа
                existingOrder.StatusId = order.StatusId;
                existingOrder.OrderTypeId = order.OrderTypeId;
                existingOrder.UserId = order.UserId;

                // Пересчитываем общую сумму на основе существующих ComputerOrders
                existingOrder.TotalAmount = existingOrder.ComputerOrders.Sum(co => co.Quantity * co.UnitPrice);

                _context.Update(existingOrder);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Заказ успешно обновлен!";
                return RedirectToAction(nameof(Details), new { id = existingOrder.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrderExists(order.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при обновлении заказа: {ex.Message}";
                await LoadViewData();
                return View(order);
            }
        }

        // GET: Order/Delete/5
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderType)
                .Include(o => o.Status)
                .Include(o => o.ComputerOrders)
                .ThenInclude(co => co.Computer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Order/Delete/5
        // POST: Order/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.ComputerOrders)
                    .Include(o => o.OrderType)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return NotFound();
                }

                var orderType = await _context.OrderTypes.FindAsync(order.OrderTypeId);
                bool isIncomeOrder = orderType?.Name?.ToLower() == "приход";

                // Корректируем склад в зависимости от типа заказа
                foreach (var computerOrder in order.ComputerOrders)
                {
                    var computer = await _context.Computers.FindAsync(computerOrder.ComputerId);
                    if (computer != null)
                    {
                        if (isIncomeOrder)
                        {
                            // Удаление прихода - уменьшаем количество
                            computer.Quantity -= computerOrder.Quantity;
                            Console.WriteLine($"Deleting income order: decreasing stock for {computer.Name} by {computerOrder.Quantity}");
                        }
                        else
                        {
                            // Удаление продажи - увеличиваем количество
                            computer.Quantity += computerOrder.Quantity;
                            Console.WriteLine($"Deleting sale order: increasing stock for {computer.Name} by {computerOrder.Quantity}");
                        }
                        _context.Computers.Update(computer);
                    }
                }

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Заказ успешно удален! Склад скорректирован.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при удалении заказа: {ex.Message}";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        // POST: Order/Complete/5 - Завершить заказ (для менеджера)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Менеджер")]
        public async Task<IActionResult> Complete(int id)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order != null)
                {
                    order.StatusId = 1; // Завершен
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Заказ отмечен как выполненный!";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при завершении заказа: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Order/Cancel/5 - Отменить заказ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.ComputerOrders)
                    .Include(o => o.OrderType)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return NotFound();
                }

                // Проверяем права доступа
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var userRole = User.FindFirst(ClaimTypes.Role).Value;

                if (userRole != "Админ" && userRole != "Менеджер" && order.UserId != userId)
                {
                    return RedirectToAction("AccessDenied", "Auth");
                }

                var orderType = await _context.OrderTypes.FindAsync(order.OrderTypeId);
                bool isIncomeOrder = orderType?.Name?.ToLower() == "приход";

                // Корректируем склад при отмене заказа
                foreach (var computerOrder in order.ComputerOrders)
                {
                    var computer = await _context.Computers.FindAsync(computerOrder.ComputerId);
                    if (computer != null)
                    {
                        if (isIncomeOrder)
                        {
                            // Отмена прихода - уменьшаем количество
                            computer.Quantity -= computerOrder.Quantity;
                            Console.WriteLine($"Canceling income order: decreasing stock for {computer.Name} by {computerOrder.Quantity}");
                        }
                        else
                        {
                            // Отмена продажи - увеличиваем количество
                            computer.Quantity += computerOrder.Quantity;
                            Console.WriteLine($"Canceling sale order: increasing stock for {computer.Name} by {computerOrder.Quantity}");
                        }
                        _context.Computers.Update(computer);
                    }
                }

                order.StatusId = 5; // Отменен
                _context.Update(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Заказ успешно отменен! Склад скорректирован.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при отмене заказа: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }

        private async Task LoadViewData()
        {
            ViewBag.Users = await _context.Users.ToListAsync();
            ViewBag.OrderTypes = await _context.OrderTypes.ToListAsync();
            ViewBag.Statuses = await _context.Statuses.ToListAsync();
            ViewBag.Computers = await _context.Computers.Where(c => c.Quantity > 0).ToListAsync();
        }
    }
}