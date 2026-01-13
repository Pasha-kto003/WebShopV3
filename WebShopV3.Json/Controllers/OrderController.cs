using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;
using Order = WebShopV3.Json.Models.Order;

namespace WebShopV3.Json.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly JsonDataService _jsonData;

        public OrderController(JsonDataService jsonData)
        {
            _jsonData = jsonData;
        }

        // GET: Order
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Index()
        {
            var orders = await GetOrdersWithDetails();
            return View(orders);
        }

        // GET: Order/MyOrders - Личные заказы пользователя
        public async Task<IActionResult> MyOrders()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var orders = await GetUserOrdersWithDetails(userId);
            return View(orders);
        }

        // GET: Order/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var order = await GetOrderWithDetails(id.Value);
            if (order == null) return NotFound();

            // Проверка прав доступа
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Create(Order order, int[] selectedComputers, int[] computerQuantities, int[] selectedComponents, int[] componentQuantities)
        {
            // Проверяем наличие данных
            if ((selectedComputers == null || computerQuantities == null) &&
                (selectedComponents == null || componentQuantities == null))
            {
                TempData["ErrorMessage"] = "Не выбрано ни одного товара для заказа";
                await LoadViewData();
                return View(order);
            }

            // Фильтруем компьютеры с количеством > 0
            var validComputers = new List<(int ComputerId, int Quantity)>();
            if (selectedComputers != null && computerQuantities != null)
            {
                for (int i = 0; i < selectedComputers.Length; i++)
                {
                    if (i < computerQuantities.Length && computerQuantities[i] > 0)
                    {
                        validComputers.Add((selectedComputers[i], computerQuantities[i]));
                    }
                }
            }

            // Фильтруем комплектующие с количеством > 0
            var validComponents = new List<(int ComponentId, int Quantity)>();
            if (selectedComponents != null && componentQuantities != null)
            {
                for (int i = 0; i < selectedComponents.Length; i++)
                {
                    if (i < componentQuantities.Length && componentQuantities[i] > 0)
                    {
                        validComponents.Add((selectedComponents[i], componentQuantities[i]));
                    }
                }
            }

            // Проверяем, есть ли выбранные товары
            if (!validComputers.Any() && !validComponents.Any())
            {
                TempData["ErrorMessage"] = "Не выбрано ни одного товара с количеством больше 0";
                await LoadViewData();
                return View(order);
            }

            // Получаем тип заказа
            var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
            var orderType = orderTypes.FirstOrDefault(ot => ot.Id == order.OrderTypeId);
            bool isIncomeOrder = orderType?.Name?.ToLower() == "приход";

            // Для заказов типа "Продажа" проверяем наличие товаров на складе
            var stockErrors = new List<string>();
            if (!isIncomeOrder)
            {
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var components = await _jsonData.GetAllAsync<Component>("Components");

                // Проверка компьютеров
                foreach (var (computerId, quantity) in validComputers)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == computerId);
                    if (computer == null)
                    {
                        stockErrors.Add($"Компьютер с ID {computerId} не найден");
                    }
                    else if (computer.Quantity < quantity)
                    {
                        stockErrors.Add($"Недостаточно компьютера '{computer.Name}' в наличии. Доступно: {computer.Quantity}, Заказано: {quantity}");
                    }
                }

                // Проверка комплектующих
                foreach (var (componentId, quantity) in validComponents)
                {
                    var component = components.FirstOrDefault(c => c.Id == componentId);
                    if (component == null)
                    {
                        stockErrors.Add($"Комплектующее с ID {componentId} не найден");
                    }
                    else if (component.Quantity < quantity)
                    {
                        stockErrors.Add($"Недостаточно комплектующего '{component.Name}' в наличии. Доступно: {component.Quantity}, Заказано: {quantity}");
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
                await _jsonData.CreateAsync("Orders", order);

                decimal totalAmount = 0;
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var components = await _jsonData.GetAllAsync<Component>("Components");
                var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
                var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");

                // Обрабатываем компьютеры
                foreach (var (computerId, quantity) in validComputers)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == computerId);
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

                        // Обновляем склад в зависимости от типа заказа
                        if (isIncomeOrder)
                        {
                            // приход - увеличиваем количество
                            computer.Quantity += quantity;
                        }
                        else
                        {
                            // Продажа - уменьшаем количество
                            computer.Quantity -= quantity;
                        }

                        await _jsonData.UpdateAsync("Computers", computer);
                        computerOrders.Add(computerOrder);
                    }
                }

                // Обрабатываем комплектующие
                foreach (var (componentId, quantity) in validComponents)
                {
                    var component = components.FirstOrDefault(c => c.Id == componentId);
                    if (component != null)
                    {
                        var componentOrder = new ComponentOrder
                        {
                            OrderId = order.Id,
                            ComponentId = componentId,
                            Quantity = quantity,
                            UnitPrice = component.Price
                        };

                        var itemTotal = component.Price * quantity;
                        totalAmount += itemTotal;

                        // Обновляем склад в зависимости от типа заказа
                        if (isIncomeOrder)
                        {
                            // приход - увеличиваем количество
                            component.Quantity += quantity;
                        }
                        else
                        {
                            // Продажа - уменьшаем количество
                            component.Quantity -= quantity;
                        }

                        await _jsonData.UpdateAsync("Components", component);
                        componentOrders.Add(componentOrder);
                    }
                }

                // Обновляем сумму заказа
                order.TotalAmount = totalAmount;
                await _jsonData.UpdateAsync("Orders", order);

                // Сохраняем ComputerOrders и ComponentOrders
                await _jsonData.SaveAllAsync("ComputerOrders", computerOrders);
                await _jsonData.SaveAllAsync("ComponentOrders", componentOrders);

                var orderTypeName = isIncomeOrder ? "приход" : "продажа";
                var itemSummary = new List<string>();

                if (validComputers.Any())
                {
                    itemSummary.Add($"{validComputers.Count} компьютеров");
                }
                if (validComponents.Any())
                {
                    itemSummary.Add($"{validComponents.Count} комплектующих");
                }

                var itemsText = string.Join(" и ", itemSummary);

                TempData["SuccessMessage"] = $"Заказ #{order.Id} ({orderTypeName}) успешно создан! " +
                                            $"{itemsText} на сумму {order.TotalAmount:C}";

                return RedirectToAction("Details", new { id = order.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при создании заказа: {ex.Message}";
                await LoadViewData();
                return View(order);
            }
        }

        // GET: Order/Edit/5
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var order = await GetOrderWithDetails(id.Value);
            if (order == null) return NotFound();

            await LoadViewData();
            return View(order);
        }

        // POST: Order/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Edit(int id, Order order)
        {
            if (id != order.Id) return NotFound();

            try
            {
                var existingOrder = await _jsonData.GetByIdAsync<Order>("Orders", id);
                if (existingOrder == null) return NotFound();

                // Получаем старый и новый тип заказа
                var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
                var oldOrderType = orderTypes.FirstOrDefault(ot => ot.Id == existingOrder.OrderTypeId);
                var newOrderType = orderTypes.FirstOrDefault(ot => ot.Id == order.OrderTypeId);

                bool wasIncomeOrder = oldOrderType?.Name?.ToLower() == "приход";
                bool isIncomeOrder = newOrderType?.Name?.ToLower() == "приход";

                // Если тип заказа изменился, нужно скорректировать склад
                if (wasIncomeOrder != isIncomeOrder)
                {
                    var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
                    var computers = await _jsonData.GetAllAsync<Computer>("Computers");

                    var orderComputerOrders = computerOrders.Where(co => co.OrderId == id).ToList();

                    foreach (var computerOrder in orderComputerOrders)
                    {
                        var computer = computers.FirstOrDefault(c => c.Id == computerOrder.ComputerId);
                        if (computer != null)
                        {
                            if (wasIncomeOrder && !isIncomeOrder)
                            {
                                // Было "приход", стало "Продажа" - убираем двойное количество
                                computer.Quantity -= computerOrder.Quantity * 2;
                            }
                            else if (!wasIncomeOrder && isIncomeOrder)
                            {
                                // Было "Продажа", стало "приход" - добавляем двойное количество
                                computer.Quantity += computerOrder.Quantity * 2;
                            }
                            await _jsonData.UpdateAsync("Computers", computer);
                        }
                    }
                }

                // Обновляем основные данные заказа
                existingOrder.StatusId = order.StatusId;
                existingOrder.OrderTypeId = order.OrderTypeId;
                existingOrder.UserId = order.UserId;

                // Пересчитываем общую сумму на основе существующих ComputerOrders
                var computerOrdersForOrder = await GetComputerOrdersForOrder(id);
                var componentOrdersForOrder = await GetComponentOrdersForOrder(id);

                existingOrder.TotalAmount = computerOrdersForOrder.Sum(co => co.Quantity * co.UnitPrice) +
                                          componentOrdersForOrder.Sum(co => co.Quantity * co.UnitPrice);

                await _jsonData.UpdateAsync("Orders", existingOrder);

                TempData["SuccessMessage"] = "Заказ успешно обновлен!";
                return RedirectToAction(nameof(Details), new { id = existingOrder.Id });
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
            if (id == null) return NotFound();

            var order = await GetOrderWithDetails(id.Value);
            if (order == null) return NotFound();

            return View(order);
        }

        // POST: Order/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var order = await _jsonData.GetByIdAsync<Order>("Orders", id);
                if (order == null) return NotFound();

                var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
                var orderType = orderTypes.FirstOrDefault(ot => ot.Id == order.OrderTypeId);
                bool isIncomeOrder = orderType?.Name?.ToLower() == "приход";

                var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
                var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var components = await _jsonData.GetAllAsync<Component>("Components");

                // Корректируем склад в зависимости от типа заказа
                var orderComputerOrders = computerOrders.Where(co => co.OrderId == id).ToList();
                var orderComponentOrders = componentOrders.Where(co => co.OrderId == id).ToList();

                foreach (var computerOrder in orderComputerOrders)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == computerOrder.ComputerId);
                    if (computer != null)
                    {
                        if (isIncomeOrder)
                        {
                            // Удаление прихода - уменьшаем количество
                            computer.Quantity -= computerOrder.Quantity;
                        }
                        else
                        {
                            // Удаление продажи - увеличиваем количество
                            computer.Quantity += computerOrder.Quantity;
                        }
                        await _jsonData.UpdateAsync("Computers", computer);
                    }
                }

                foreach (var componentOrder in orderComponentOrders)
                {
                    var component = components.FirstOrDefault(c => c.Id == componentOrder.ComponentId);
                    if (component != null)
                    {
                        if (isIncomeOrder)
                        {
                            // Удаление прихода - уменьшаем количество
                            component.Quantity -= componentOrder.Quantity;
                        }
                        else
                        {
                            // Удаление продажи - увеличиваем количество
                            component.Quantity += componentOrder.Quantity;
                        }
                        await _jsonData.UpdateAsync("Components", component);
                    }
                }

                // Удаляем заказ и его связанные записи
                await DeleteOrderAndRelatedData(id);

                TempData["SuccessMessage"] = "Заказ успешно удален! Склад скорректирован.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при удалении заказа: {ex.Message}";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        // POST: Order/Complete/5 - Завершить заказ
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Менеджер")]
        public async Task<IActionResult> Complete(int id)
        {
            try
            {
                var order = await _jsonData.GetByIdAsync<Order>("Orders", id);
                if (order != null)
                {
                    order.StatusId = 4; // Завершен
                    await _jsonData.UpdateAsync("Orders", order);
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
                var order = await _jsonData.GetByIdAsync<Order>("Orders", id);
                if (order == null) return NotFound();

                // Проверяем права доступа
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var userRole = User.FindFirst(ClaimTypes.Role).Value;

                if (userRole != "Админ" && userRole != "Менеджер" && order.UserId != userId)
                {
                    return RedirectToAction("AccessDenied", "Auth");
                }

                var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
                var orderType = orderTypes.FirstOrDefault(ot => ot.Id == order.OrderTypeId);
                bool isIncomeOrder = orderType?.Name?.ToLower() == "приход";

                var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
                var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var components = await _jsonData.GetAllAsync<Component>("Components");

                // Корректируем склад при отмене заказа
                var orderComputerOrders = computerOrders.Where(co => co.OrderId == id).ToList();
                var orderComponentOrders = componentOrders.Where(co => co.OrderId == id).ToList();

                foreach (var computerOrder in orderComputerOrders)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == computerOrder.ComputerId);
                    if (computer != null)
                    {
                        if (isIncomeOrder)
                        {
                            // Отмена прихода - уменьшаем количество
                            computer.Quantity -= computerOrder.Quantity;
                        }
                        else
                        {
                            // Отмена продажи - увеличиваем количество
                            computer.Quantity += computerOrder.Quantity;
                        }
                        await _jsonData.UpdateAsync("Computers", computer);
                    }
                }

                foreach (var componentOrder in orderComponentOrders)
                {
                    var component = components.FirstOrDefault(c => c.Id == componentOrder.ComponentId);
                    if (component != null)
                    {
                        if (isIncomeOrder)
                        {
                            // Отмена прихода - уменьшаем количество
                            component.Quantity -= componentOrder.Quantity;
                        }
                        else
                        {
                            // Отмена продажи - увеличиваем количество
                            component.Quantity += componentOrder.Quantity;
                        }
                        await _jsonData.UpdateAsync("Components", component);
                    }
                }

                order.StatusId = 5; // Отменен
                await _jsonData.UpdateAsync("Orders", order);

                TempData["SuccessMessage"] = "Заказ успешно отменен! Склад скорректирован.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при отмене заказа: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnToStock(int orderId)
        {
            try
            {
                var order = await GetOrderWithDetails(orderId);
                if (order == null)
                    return Json(new { success = false, message = "Заказ не найден" });

                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var components = await _jsonData.GetAllAsync<Component>("Components");

                // Возвращаем товары на склад
                foreach (var computerOrder in order.ComputerOrders)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == computerOrder.ComputerId);
                    if (computer != null)
                    {
                        computer.Quantity += computerOrder.Quantity;
                        await _jsonData.UpdateAsync("Computers", computer);
                    }
                }

                foreach (var componentOrder in order.ComponentOrders)
                {
                    var component = components.FirstOrDefault(c => c.Id == componentOrder.ComponentId);
                    if (component != null)
                    {
                        component.Quantity += componentOrder.Quantity;
                        await _jsonData.UpdateAsync("Components", component);
                    }
                }

                await _jsonData.UpdateAsync("Orders", order);
                return Json(new { success = true, message = "Товары возвращены на склад" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        private async Task<bool> OrderExists(int id)
        {
            var order = await _jsonData.GetByIdAsync<Order>("Orders", id);
            return order != null;
        }

        private async Task LoadViewData()
        {
            var users = await _jsonData.GetAllAsync<User>("Users");
            var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
            var statuses = await _jsonData.GetAllAsync<Status>("Statuses");
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");

            ViewBag.Users = users;
            ViewBag.OrderTypes = orderTypes;
            ViewBag.Statuses = statuses;
            ViewBag.Computers = computers.Where(c => c.Quantity > 0 || c.Quantity == 0).ToList();
            ViewBag.Components = components.Where(c => c.Quantity > 0 || c.Quantity == 0).ToList();
        }

        // Вспомогательные методы

        private async Task<List<Order>> GetOrdersWithDetails()
        {
            var orders = await _jsonData.GetAllAsync<Order>("Orders");
            var users = await _jsonData.GetAllAsync<User>("Users");
            var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
            var statuses = await _jsonData.GetAllAsync<Status>("Statuses");
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");
            var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
            var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");

            foreach (var order in orders)
            {
                order.User = users.FirstOrDefault(u => u.Id == order.UserId);
                order.OrderType = orderTypes.FirstOrDefault(ot => ot.Id == order.OrderTypeId);
                order.Status = statuses.FirstOrDefault(s => s.Id == order.StatusId);

                // Загружаем ComputerOrders
                var coList = computerOrders.Where(co => co.OrderId == order.Id).ToList();
                foreach (var co in coList)
                {
                    co.Computer = computers.FirstOrDefault(c => c.Id == co.ComputerId);
                }
                order.ComputerOrders = coList;

                // Загружаем ComponentOrders
                var compoList = componentOrders.Where(co => co.OrderId == order.Id).ToList();
                foreach (var co in compoList)
                {
                    co.Component = components.FirstOrDefault(c => c.Id == co.ComponentId);
                }
                order.ComponentOrders = compoList;
            }

            return orders.OrderByDescending(o => o.OrderDate).ToList();
        }

        private async Task<List<Order>> GetUserOrdersWithDetails(int userId)
        {
            var allOrders = await GetOrdersWithDetails();
            return allOrders.Where(o => o.UserId == userId).ToList();
        }

        private async Task<Order?> GetOrderWithDetails(int orderId)
        {
            var order = await _jsonData.GetByIdAsync<Order>("Orders", orderId);
            if (order == null) return null;

            var users = await _jsonData.GetAllAsync<User>("Users");
            var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
            var statuses = await _jsonData.GetAllAsync<Status>("Statuses");
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");
            var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
            var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");

            order.User = users.FirstOrDefault(u => u.Id == order.UserId);
            order.OrderType = orderTypes.FirstOrDefault(ot => ot.Id == order.OrderTypeId);
            order.Status = statuses.FirstOrDefault(s => s.Id == order.StatusId);

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

        private async Task<List<ComputerOrder>> GetComputerOrdersForOrder(int orderId)
        {
            var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
            return computerOrders.Where(co => co.OrderId == orderId).ToList();
        }

        private async Task<List<ComponentOrder>> GetComponentOrdersForOrder(int orderId)
        {
            var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");
            return componentOrders.Where(co => co.OrderId == orderId).ToList();
        }

        private async Task DeleteOrderAndRelatedData(int orderId)
        {
            // Удаляем ComputerOrders
            var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
            var computerOrdersToRemove = computerOrders.Where(co => co.OrderId == orderId).ToList();
            foreach (var co in computerOrdersToRemove)
            {
                computerOrders.Remove(co);
            }
            await _jsonData.SaveAllAsync("ComputerOrders", computerOrders);

            // Удаляем ComponentOrders
            var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");
            var componentOrdersToRemove = componentOrders.Where(co => co.OrderId == orderId).ToList();
            foreach (var co in componentOrdersToRemove)
            {
                componentOrders.Remove(co);
            }
            await _jsonData.SaveAllAsync("ComponentOrders", componentOrders);

            // Удаляем сам заказ
            await _jsonData.DeleteAsync<Order>("Orders", orderId);
        }
    }
}