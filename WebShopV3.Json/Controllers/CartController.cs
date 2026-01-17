using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;
using Order = WebShopV3.Json.Models.Order;

namespace WebShopV3.Json.Controllers
{
    public class CartController : Controller
    {
        private readonly JsonDataService _jsonData;
        private const string CartSessionKey = "Cart";
        private readonly IYookassaService _yookassaService;
        private readonly ILogger<CartController> _logger;
        private readonly AuthService _authService;

        public CartController(
            JsonDataService jsonData,
            IYookassaService yookassaService,
            ILogger<CartController> logger,
            AuthService authService)
        {
            _jsonData = jsonData;
            _yookassaService = yookassaService;
            _logger = logger;
            _authService = authService;
        }

        // GET: Cart
        public IActionResult Index()
        {
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            return View(cart);
        }

        // POST: Cart/AddToCart
        [HttpPost]
        public async Task<IActionResult> AddToCart(int? computerId, int? componentId, int quantity = 1)
        {
            try
            {
                if (!computerId.HasValue && !componentId.HasValue)
                {
                    return Json(new { success = false, message = "Не указан товар для добавления" });
                }

                var cartJson = HttpContext.Session.GetString(CartSessionKey);
                var cart = string.IsNullOrEmpty(cartJson)
                    ? new Cart()
                    : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

                string productName = "";
                bool success = false;

                if (computerId.HasValue)
                {
                    success = await AddComputerToCart(computerId.Value, quantity, cart);
                    if (success)
                    {
                        var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                        var computer = computers.FirstOrDefault(c => c.Id == computerId.Value);
                        productName = computer?.Name ?? "";
                    }
                }
                else if (componentId.HasValue)
                {
                    success = await AddComponentToCart(componentId.Value, quantity, cart);
                    if (success)
                    {
                        var components = await _jsonData.GetAllAsync<Component>("Components");
                        var component = components.FirstOrDefault(c => c.Id == componentId.Value);
                        productName = component?.Name ?? "";
                    }
                }

                if (success)
                {
                    var updatedCartJson = JsonSerializer.Serialize(cart);
                    HttpContext.Session.SetString(CartSessionKey, updatedCartJson);

                    return Json(new
                    {
                        success = true,
                        message = $"\"{productName}\" добавлен в корзину!",
                        cartItems = cart.TotalItems,
                        cartTotal = cart.TotalAmount
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = "Не удалось добавить товар в корзину. Возможно, недостаточно товара на складе."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to cart");
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        private async Task<bool> AddComputerToCart(int computerId, int quantity, Cart cart)
        {
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var computer = computers.FirstOrDefault(c => c.Id == computerId);

            if (computer == null || computer.Quantity < quantity)
            {
                return false;
            }

            var existingItem = cart.Items.FirstOrDefault(x => x.ComputerId == computerId && x.IsComputer);

            if (existingItem != null)
            {
                if (computer.Quantity < existingItem.Quantity + quantity)
                {
                    return false;
                }
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ComputerId = computerId,
                    Name = computer.Name,
                    Price = computer.Price,
                    Quantity = quantity,
                    ImageUrl = computer.ImageUrl
                });
            }

            return true;
        }

        private async Task<bool> AddComponentToCart(int componentId, int quantity, Cart cart)
        {
            var components = await _jsonData.GetAllAsync<Component>("Components");
            var component = components.FirstOrDefault(c => c.Id == componentId);

            if (component == null || component.Quantity < quantity)
            {
                return false;
            }

            var existingItem = cart.Items.FirstOrDefault(x => x.ComponentId == componentId && x.IsComponent);

            if (existingItem != null)
            {
                if (component.Quantity < existingItem.Quantity + quantity)
                {
                    return false;
                }
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ComponentId = componentId,
                    Name = component.Name,
                    Price = component.Price,
                    Quantity = quantity,
                    ImageUrl = component.ImageUrl
                });
            }

            return true;
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int? computerId, int? componentId, int quantity)
        {
            if (quantity <= 0)
            {
                return RemoveFromCart(computerId, componentId);
            }

            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            CartItem item = null;
            if (computerId.HasValue)
            {
                item = cart.Items.FirstOrDefault(x => x.ComputerId == computerId && x.IsComputer);
            }
            else if (componentId.HasValue)
            {
                item = cart.Items.FirstOrDefault(x => x.ComponentId == componentId && x.IsComponent);
            }

            if (item != null)
            {
                // Проверяем наличие
                if (computerId.HasValue)
                {
                    var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                    var computer = computers.FirstOrDefault(c => c.Id == computerId.Value);
                    if (computer == null || computer.Quantity < quantity)
                    {
                        return Json(new { success = false, message = "Недостаточно товара в наличии" });
                    }
                }
                else if (componentId.HasValue)
                {
                    var components = await _jsonData.GetAllAsync<Component>("Components");
                    var component = components.FirstOrDefault(c => c.Id == componentId.Value);
                    if (component == null || component.Quantity < quantity)
                    {
                        return Json(new { success = false, message = "Недостаточно товара в наличии" });
                    }
                }

                item.Quantity = quantity;
                var updatedCartJson = JsonSerializer.Serialize(cart);
                HttpContext.Session.SetString(CartSessionKey, updatedCartJson);

                return Json(new
                {
                    success = true,
                    totalItems = cart.TotalItems,
                    totalAmount = cart.TotalAmount,
                    itemTotal = item.TotalPrice
                });
            }

            return Json(new { success = false, message = "Товар не найден в корзине" });
        }

        // POST: Cart/RemoveFromCart
        [HttpPost]
        public IActionResult RemoveFromCart(int? computerId, int? componentId)
        {
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            CartItem item = null;
            if (computerId.HasValue)
            {
                item = cart.Items.FirstOrDefault(x => x.ComputerId == computerId && x.IsComputer);
            }
            else if (componentId.HasValue)
            {
                item = cart.Items.FirstOrDefault(x => x.ComponentId == componentId && x.IsComponent);
            }

            if (item != null)
            {
                cart.Items.Remove(item);
                var updatedCartJson = JsonSerializer.Serialize(cart);
                HttpContext.Session.SetString(CartSessionKey, updatedCartJson);

                return Json(new
                {
                    success = true,
                    message = "Товар удален из корзины",
                    totalItems = cart.TotalItems,
                    totalAmount = cart.TotalAmount
                });
            }

            return Json(new { success = false, message = "Товар не найден в корзине" });
        }

        // POST: Cart/Clear
        [HttpPost]
        public IActionResult Clear()
        {
            HttpContext.Session.Remove(CartSessionKey);
            return Json(new { success = true, message = "Корзина очищена" });
        }

        // GET: Cart/GetSimilarComputers
        [HttpGet]
        public async Task<IActionResult> GetSimilarComputers()
        {
            try
            {
                var cartJson = HttpContext.Session.GetString(CartSessionKey);
                var cart = string.IsNullOrEmpty(cartJson)
                    ? new Cart()
                    : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

                var recommendations = new List<object>();

                var computerIdsInCart = cart.Items
                    .Where(item => item.IsComputer)
                    .Select(item => item.ComputerId)
                    .ToList();

                if (computerIdsInCart.Any())
                {
                    var firstComputerId = computerIdsInCart.First();
                    var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                    var cartComputer = computers.FirstOrDefault(c => c.Id == firstComputerId);

                    if (cartComputer != null)
                    {
                        var minPrice = cartComputer.Price * 0.8m;
                        var maxPrice = cartComputer.Price * 1.2m;

                        var similarByPrice = computers
                            .Where(c => c.Id != firstComputerId &&
                                       c.Quantity > 0 &&
                                       c.Price >= minPrice &&
                                       c.Price <= maxPrice)
                            .OrderBy(c => Guid.NewGuid())
                            .Take(4)
                            .Select(c => new
                            {
                                c.Id,
                                c.Name,
                                c.Price,
                                ImageUrl = string.IsNullOrEmpty(c.ImageUrl) ? "default-computer.jpg" : c.ImageUrl,
                                c.Description,
                                Category = "Похожая цена"
                            })
                            .ToList();

                        recommendations.AddRange(similarByPrice);
                    }
                }

                if (recommendations.Count < 4)
                {
                    var randomCount = 4 - recommendations.Count;
                    var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                    var randomComputers = computers
                        .Where(c => c.Quantity > 0)
                        .OrderBy(c => Guid.NewGuid())
                        .Take(randomCount)
                        .Select(c => new
                        {
                            c.Id,
                            c.Name,
                            c.Price,
                            ImageUrl = string.IsNullOrEmpty(c.ImageUrl) ? "default-computer.jpg" : c.ImageUrl,
                            c.Description,
                            Category = "Рекомендуем"
                        })
                        .ToList();

                    recommendations.AddRange(randomComputers);
                }

                return Json(new
                {
                    success = true,
                    recommendations = recommendations.Take(4).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSimilarComputers");
                return Json(new
                {
                    success = true,
                    recommendations = new List<object>()
                });
            }
        }

        // GET: Cart/Checkout
        public async Task<IActionResult> Checkout()
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Для оформления заказа необходимо войти в систему";
                return RedirectToAction("Login", "Auth");
            }

            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            if (!cart.Items.Any())
            {
                TempData["ErrorMessage"] = "Корзина пуста";
                return RedirectToAction("Index");
            }

            try
            {
                var userId = _authService.GetCurrentUserId(User);
                var users = await _jsonData.GetAllAsync<User>("Users");
                var user = users.FirstOrDefault(u => u.Id == userId);

                if (user != null)
                {
                    ViewBag.UserData = new
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        Phone = user.Phone
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось загрузить данные пользователя для Checkout");
            }

            return View(cart);
        }

        // POST: Cart/ProcessYookassaPayment - для онлайн оплаты через ЮКассу
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessYookassaPayment(
    string firstName, string lastName, string email, string phone, string address, string comment)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Для оформления заказа необходимо войти в систему";
                return RedirectToAction("Login", "Auth");
            }

            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            if (!cart.Items.Any())
            {
                TempData["ErrorMessage"] = "Корзина пуста";
                return RedirectToAction("Index");
            }

            try
            {
                var userId = _authService.GetCurrentUserId(User);

                // Проверка наличия товаров
                var stockErrors = await CheckStockAvailability(cart);
                if (stockErrors.Any())
                {
                    TempData["ErrorMessage"] = string.Join("<br>", stockErrors);
                    return RedirectToAction("Checkout");
                }

                // Резервируем товары (уменьшаем Quantity в Components/Computers)
                await ReserveItems(cart);

                // Загружаем справочники
                var statuses = await _jsonData.GetAllAsync<Status>("OrderStatuses");
                var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");

                var pendingStatus = statuses.FirstOrDefault(s => s.Name == "В ожидании")
                    ?? new Status { Id = 1, Name = "В ожидании" };

                var saleOrderType = orderTypes.FirstOrDefault(ot => ot.Name == "Продажа")
                    ?? new OrderType { Id = 1, Name = "Продажа" };

                var orderId = await GetNextOrderIdAsync();

                // Создаём заказ
                var order = new Order
                {
                    Id = orderId,
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    OrderTypeId = saleOrderType.Id,
                    StatusId = pendingStatus.Id,
                    TotalAmount = cart.TotalAmount,
                    Description = GenerateOrderDescription(address, comment),
                };

                await _jsonData.CreateAsync("Orders", order);

                // === РАБОТА С КРОСС-ТАБЛИЦАМИ ===

                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var components = await _jsonData.GetAllAsync<Component>("Components");

                // Сохраняем связи: ComputerOrder
                foreach (var cartItem in cart.Items.Where(i => i.IsComputer && i.ComputerId != 0))
                {
                    var computer = computers.FirstOrDefault(c => c.Id == cartItem.ComputerId);
                    var computerOrder = new ComputerOrder
                    {
                        OrderId = orderId,
                        ComputerId = cartItem.ComputerId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Price
                    };
                    await _jsonData.CreateAsync("ComputerOrder", computerOrder);
                }

                // Сохраняем связи: ComponentOrder
                foreach (var cartItem in cart.Items.Where(i => i.IsComponent && i.ComponentId != 0))
                {
                    var component = components.FirstOrDefault(c => c.Id == cartItem.ComponentId);
                    var componentOrder = new ComponentOrder
                    {
                        OrderId = orderId,
                        ComponentId = cartItem.ComponentId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Price
                    };
                    await _jsonData.CreateAsync("ComponentOrder", componentOrder);
                }

                // === СОЗДАНИЕ ПЛАТЕЖА ===

                var paymentRequest = new Models.PaymentRequest
                {
                    OrderId = orderId,
                    Amount = cart.TotalAmount,
                    Description = $"Оплата заказа #{orderId}",
                    ReturnUrl = Url.Action("PaymentSuccess", "Payment", new { orderId }, Request.Scheme) ??
                               $"{Request.Scheme}://{Request.Host}/Payment/PaymentSuccess?orderId={orderId}"
                };

                var paymentResponse = await _yookassaService.CreatePaymentAsync(paymentRequest);
                await _jsonData.UpdateAsync("Orders", order);

                // Сохраняем платёж
                var paymentRecord = new PaymentResponse
                {
                    Id = paymentResponse.Id,
                    Amount = paymentResponse.Amount,
                    Status = paymentResponse.Status,
                    ConfirmationUrl = paymentResponse.ConfirmationUrl,
                };
                await _jsonData.CreateAsync("Payments", paymentRecord);

                // Очищаем корзину
                HttpContext.Session.Remove(CartSessionKey);

                return Redirect(paymentResponse.ConfirmationUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessYookassaPayment");

                // Восстанавливаем остатки при ошибке
                await RestoreReservedItems(cart);

                TempData["ErrorMessage"] = $"Ошибка при создании платежа: {ex.Message}";
                return RedirectToAction("Checkout");
            }
        }

        // Вспомогательные методы для работы с Yookassa

        private async Task ReserveItems(Cart cart)
        {
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");
            bool changed = false;

            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == item.ComputerId);
                    if (computer != null && computer.Quantity >= item.Quantity)
                    {
                        computer.Quantity -= item.Quantity;
                        changed = true;
                    }
                }
                else if (item.IsComponent)
                {
                    var component = components.FirstOrDefault(c => c.Id == item.ComponentId);
                    if (component != null && component.Quantity >= item.Quantity)
                    {
                        component.Quantity -= item.Quantity;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                await _jsonData.SaveAllAsync("Computers", computers);
                await _jsonData.SaveAllAsync("Components", components);
            }
        }

        private async Task RestoreReservedItems(Cart cart)
        {
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");
            bool changed = false;

            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == item.ComputerId);
                    if (computer != null)
                    {
                        computer.Quantity += item.Quantity;
                        changed = true;
                    }
                }
                else if (item.IsComponent)
                {
                    var component = components.FirstOrDefault(c => c.Id == item.ComponentId);
                    if (component != null)
                    {
                        component.Quantity += item.Quantity;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                await _jsonData.SaveAllAsync("Computers", computers);
                await _jsonData.SaveAllAsync("Components", components);
            }
        }

        private async Task<int> GetNextOrderIdAsync()
        {
            var orders = await _jsonData.GetAllAsync<Order>("Orders");
            return orders.Any() ? orders.Max(o => o.Id) + 1 : 1;
        }

        // POST: Cart/CompleteOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteOrder(string firstName, string lastName, string email, string phone, string address, string comment)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Для оформления заказа необходимо войти в систему";
                return RedirectToAction("Login", "Auth");
            }

            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            if (!cart.Items.Any())
            {
                TempData["ErrorMessage"] = "Корзина пуста";
                return RedirectToAction("Index");
            }

            try
            {
                var userId = _authService.GetCurrentUserId(User);

                // Проверка наличия товаров
                var stockErrors = await CheckStockAvailability(cart);
                if (stockErrors.Any())
                {
                    TempData["ErrorMessage"] = string.Join("<br>", stockErrors);
                    return RedirectToAction("Checkout");
                }

                // Получаем ID для типа заказа и статуса
                var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
                var statuses = await _jsonData.GetAllAsync<Status>("Statuses");

                var saleOrderType = orderTypes.FirstOrDefault(ot => ot.Name == "Продажа");
                var completedStatus = statuses.FirstOrDefault(s => s.Name == "Выполнен");

                if (saleOrderType == null || completedStatus == null)
                {
                    TempData["ErrorMessage"] = "Ошибка конфигурации системы";
                    return RedirectToAction("Checkout");
                }

                // Создаем заказ
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.Now,
                    OrderTypeId = saleOrderType.Id,
                    StatusId = completedStatus.Id,
                    TotalAmount = cart.TotalAmount,
                    Description = GenerateOrderDescription(address, comment)
                };

                await _jsonData.CreateAsync("Orders", order);

                // Обрабатываем товары в заказе
                await ProcessOrderItems(cart, order.Id);

                // Очищаем корзину
                HttpContext.Session.Remove(CartSessionKey);

                TempData["SuccessMessage"] = $"Заказ #{order.Id} успешно оформлен! Сумма: {order.TotalAmount:C}";
                return RedirectToAction("MyOrders", "Order");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CompleteOrder");
                TempData["ErrorMessage"] = $"Ошибка при оформлении заказа: {ex.Message}";
                return RedirectToAction("Checkout");
            }
        }

        // Вспомогательные методы
        private async Task<List<string>> CheckStockAvailability(Cart cart)
        {
            var errors = new List<string>();
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");

            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == item.ComputerId);
                    if (computer == null || computer.Quantity < item.Quantity)
                    {
                        errors.Add($"Недостаточно компьютеров '{item.Name}' в наличии");
                    }
                }
                else if (item.IsComponent)
                {
                    var component = components.FirstOrDefault(c => c.Id == item.ComponentId);
                    if (component == null || component.Quantity < item.Quantity)
                    {
                        errors.Add($"Недостаточно комплектующих '{item.Name}' в наличии");
                    }
                }
            }

            return errors;
        }

        private string GenerateOrderDescription(string address, string comment)
        {
            var description = $"Адрес доставки: {address}";
            if (!string.IsNullOrEmpty(comment))
            {
                description += $". Комментарий: {comment}";
            }
            return description;
        }

        private async Task ProcessOrderItems(Cart cart, int orderId)
        {
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            var components = await _jsonData.GetAllAsync<Component>("Components");
            var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
            var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");

            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computer = computers.FirstOrDefault(c => c.Id == item.ComputerId);
                    if (computer != null)
                    {
                        // Уменьшаем количество
                        computer.Quantity -= item.Quantity;
                        await _jsonData.UpdateAsync("Computers", computer);

                        // Создаем запись о заказе компьютера
                        var computerOrder = new ComputerOrder
                        {
                            OrderId = orderId,
                            ComputerId = item.ComputerId,
                            Quantity = item.Quantity,
                            UnitPrice = item.Price
                        };

                        // Сохраняем в отдельный JSON файл
                        computerOrders.Add(computerOrder);
                    }
                }
                else if (item.IsComponent)
                {
                    var component = components.FirstOrDefault(c => c.Id == item.ComponentId);
                    if (component != null)
                    {
                        // Уменьшаем количество
                        component.Quantity -= item.Quantity;
                        await _jsonData.UpdateAsync("Components", component);

                        // Создаем запись о заказе компонента
                        var componentOrder = new ComponentOrder
                        {
                            OrderId = orderId,
                            ComponentId = item.ComponentId,
                            Quantity = item.Quantity,
                            UnitPrice = item.Price
                        };

                        componentOrders.Add(componentOrder);
                    }
                }
            }

            // Сохраняем обновленные списки заказов
            await _jsonData.SaveAllAsync("ComputerOrders", computerOrders);
            await _jsonData.SaveAllAsync("ComponentOrders", componentOrders);
        }

        // GET: Cart/GetCartCount
        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            return Json(new { count = cart.TotalItems });
        }

        // Обновите модель Order для JSON (добавить в WebShopV3.Json.Models)

    }
}