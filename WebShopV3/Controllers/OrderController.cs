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
        public IActionResult Create()
        {
            ViewBag.Users = _context.Users.ToList();
            ViewBag.OrderTypes = _context.OrderTypes.ToList();
            ViewBag.Statuses = _context.Statuses.ToList();
            ViewBag.Computers = _context.Computers.Where(c => c.Quantity > 0).ToList();

            // Пользователь может создавать только заказы для себя
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            ViewBag.OrderTypes = _context.OrderTypes.ToList();
            ViewBag.Computers = _context.Computers.Where(c => c.Quantity > 0).ToList();
            // Устанавливаем текущего пользователя
            ViewBag.CurrentUserId = userId;
            return View();
        }

        // POST: Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
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

            // Базовая настройка заказа
            order.OrderDate = DateTime.Now;
            order.TotalAmount = 0;

            // Сохраняем заказ чтобы получить ID
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            Console.WriteLine($"Order created with ID: {order.Id}");

            decimal totalAmount = 0;
            bool hasItems = false;

            // Обрабатываем компьютеры
            if (selectedComputers != null && quantities != null)
            {
                for (int i = 0; i < selectedComputers.Length; i++)
                {
                    var computerId = selectedComputers[i];
                    var quantity = quantities[i];

                    Console.WriteLine($"Processing: ComputerId={computerId}, Quantity={quantity}");

                    // Пропускаем если количество 0
                    if (quantity <= 0)
                    {
                        Console.WriteLine($"Skipping computer {computerId} - quantity is 0");
                        continue;
                    }

                    var computer = await _context.Computers.FindAsync(computerId);
                    if (computer != null && computer.Quantity >= quantity)
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
                        hasItems = true;

                        Console.WriteLine($"Added: {computer.Name}, Qty: {quantity}, Price: {computer.Price}, Total: {itemTotal}");

                        // Обновляем склад
                        computer.Quantity -= quantity;
                        _context.Computers.Update(computer);
                        _context.ComputerOrders.Add(computerOrder);
                    }
                    else
                    {
                        Console.WriteLine($"Computer {computerId} not found or not enough quantity");
                    }
                }

                // Обновляем сумму заказа
                if (hasItems)
                {
                    order.TotalAmount = totalAmount;
                    _context.Orders.Update(order);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"Final order total: {order.TotalAmount}");
                    TempData["SuccessMessage"] = $"Заказ #{order.Id} успешно создан! Сумма: {order.TotalAmount:C}";
                }
                else
                {
                    // Если нет товаров, удаляем заказ
                    _context.Orders.Remove(order);
                    await _context.SaveChangesAsync();
                    TempData["ErrorMessage"] = "Не выбрано ни одного компьютера с количеством больше 0";
                    return await LoadViewDataAndReturnView(new Order());
                }
            }
            else
            {
                // Если массивы пустые или разной длины
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] = "Ошибка в данных заказа";
                return await LoadViewDataAndReturnView(new Order());
            }

            return RedirectToAction("MyOrders");
        }

        private async Task<IActionResult> LoadViewDataAndReturnView(Order order)
        {
            ViewBag.Users = await _context.Users.ToListAsync();
            ViewBag.OrderTypes = await _context.OrderTypes.ToListAsync();
            ViewBag.Statuses = await _context.Statuses.ToListAsync();
            ViewBag.Computers = await _context.Computers.Where(c => c.Quantity > 0).ToListAsync();

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            ViewBag.CurrentUserId = userId;

            return View(order);
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
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.Users = _context.Users.ToList();
            ViewBag.OrderTypes = _context.OrderTypes.ToList();
            ViewBag.Statuses = _context.Statuses.ToList();

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
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (existingOrder == null)
                {
                    return NotFound();
                }

                existingOrder.StatusId = order.StatusId;
                existingOrder.OrderTypeId = order.OrderTypeId;
                existingOrder.UserId = order.UserId;

                existingOrder.TotalAmount = existingOrder.ComputerOrders.Sum(co => co.Quantity * co.UnitPrice);

                _context.Update(existingOrder);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Заказ успешно обновлен!";
                return RedirectToAction(nameof(Index));
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
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Order/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Заказ успешно удален!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Order/Complete/5 - Завершить заказ (для менеджера)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Менеджер")]
        public async Task<IActionResult> Complete(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                order.StatusId = 1;
                _context.Update(order);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Заказ отмечен как выполненный!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }
    }
}
