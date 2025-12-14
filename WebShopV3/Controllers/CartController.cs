using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using WebShopV3.Models;
using WebShopV3.Services;

namespace WebShopV3.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string CartSessionKey = "Cart";
        private readonly IYookassaService _yookassaService;
        private readonly ILogger<CartController> _logger;

        public CartController(
            ApplicationDbContext context,
            IYookassaService yookassaService,
            ILogger<CartController> logger)
        {
            _context = context;
            _yookassaService = yookassaService;
            _logger = logger;
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

        // POST: Cart/AddToCart - универсальный метод для компьютеров и компонентов
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
                        var computer = await _context.Computers.FindAsync(computerId.Value);
                        productName = computer?.Name ?? "";
                    }
                }
                else if (componentId.HasValue)
                {
                    success = await AddComponentToCart(componentId.Value, quantity, cart);
                    if (success)
                    {
                        var component = await _context.Components.FindAsync(componentId.Value);
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

        // Сделайте эти методы приватными в контроллере:
        private async Task<bool> AddComputerToCart(int computerId, int quantity, Cart cart)
        {
            var computer = await _context.Computers.FindAsync(computerId);
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
            var component = await _context.Components.FindAsync(componentId);
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
                    var computer = await _context.Computers.FindAsync(computerId.Value);
                    if (computer == null || computer.Quantity < quantity)
                    {
                        return Json(new { success = false, message = "Недостаточно товара в наличии" });
                    }
                }
                else if (componentId.HasValue)
                {
                    var component = await _context.Components.FindAsync(componentId.Value);
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
                    var cartComputer = await _context.Computers.FindAsync(firstComputerId);

                    if (cartComputer != null)
                    {
                        var minPrice = cartComputer.Price * 0.8m;
                        var maxPrice = cartComputer.Price * 1.2m;

                        var similarByPrice = await _context.Computers
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
                            .ToListAsync();

                        recommendations.AddRange(similarByPrice);
                    }
                }

                if (recommendations.Count < 4)
                {
                    var randomCount = 4 - recommendations.Count;
                    var randomComputers = await _context.Computers
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
                        .ToListAsync();

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

            // Проверяем наличие всех товаров
            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computer = await _context.Computers.FindAsync(item.ComputerId);
                    if (computer == null || computer.Quantity < item.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Недостаточно компьютеров '{item.Name}' в наличии. Доступно: {computer?.Quantity ?? 0}";
                        return RedirectToAction("Index");
                    }
                }
                else if (item.IsComponent)
                {
                    var component = await _context.Components.FindAsync(item.ComponentId);
                    if (component == null || component.Quantity < item.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Недостаточно комплектующих '{item.Name}' в наличии. Доступно: {component?.Quantity ?? 0}";
                        return RedirectToAction("Index");
                    }
                }
            }

            // Получаем данные пользователя
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            var user = await _context.Users.FindAsync(userId);

            ViewBag.UserData = new
            {
                user?.FirstName,
                user?.LastName,
                user?.Email,
                user?.Phone
            };

            return View(cart);
        }

        // POST: Cart/CompleteOrder - для оффлайн заказов (БЕЗ онлайн оплаты)
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
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                // Проверка наличия товаров
                var stockErrors = await CheckStockAvailability(cart);
                if (stockErrors.Any())
                {
                    TempData["ErrorMessage"] = string.Join("<br>", stockErrors);
                    return RedirectToAction("Checkout");
                }

                // Обновляем данные пользователя
                await UpdateUserInfo(userId, firstName, lastName, email, phone);

                // Создаем заказ со статусом "Завершен" сразу (для оффлайн заказов)
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.Now,
                    OrderTypeId = 3, // Продажа
                    StatusId = 4, // Завершен (для оффлайн заказов)
                    TotalAmount = cart.TotalAmount,
                    Description = GenerateOrderDescription(address, comment)
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // СПИСЫВАЕМ ТОВАРЫ И создаем записи о заказе
                await ProcessOrderItems(cart, order.Id);

                await _context.SaveChangesAsync();

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

        // POST: Cart/ProcessYookassaPayment - для онлайн оплаты через ЮКассу
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessYookassaPayment(string firstName, string lastName, string email, string phone, string address, string comment)
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
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                // Проверка наличия товаров
                var stockErrors = await CheckStockAvailability(cart);
                if (stockErrors.Any())
                {
                    TempData["ErrorMessage"] = string.Join("<br>", stockErrors);
                    return RedirectToAction("Checkout");
                }

                // РЕЗЕРВИРУЕМ товары сразу (уменьшаем количество)
                await ReserveItems(cart);

                // Обновляем данные пользователя
                await UpdateUserInfo(userId, firstName, lastName, email, phone);

                // Создаем заказ со статусом "В ожидании оплаты"
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.Now,
                    OrderTypeId = 3, // Продажа
                    StatusId = 5, // В ожидании оплаты
                    TotalAmount = cart.TotalAmount,
                    Description = GenerateOrderDescription(address, comment)
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Создаем записи о товарах в заказе (без повторного списания)
                await CreateOrderItems(cart, order.Id);

                await _context.SaveChangesAsync();

                // Создаем платеж в ЮКассе
                var paymentRequest = new PaymentRequest
                {
                    Amount = cart.TotalAmount,
                    Description = $"Оплата заказа #{order.Id}",
                    OrderId = order.Id,
                    ReturnUrl = Url.Action("Success", "Payment", new { orderId = order.Id }, Request.Scheme) ??
                               $"{Request.Scheme}://{Request.Host}/Payment/Success?orderId={order.Id}"
                };

                var paymentResponse = await _yookassaService.CreatePaymentAsync(paymentRequest);

                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                // Перенаправляем пользователя на страницу оплаты ЮКассы
                return Redirect(paymentResponse.ConfirmationUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessYookassaPayment");
                TempData["ErrorMessage"] = $"Ошибка при создании платежа: {ex.Message}";
                return RedirectToAction("Checkout");
            }
        }

        // Вспомогательные методы
        private async Task<List<string>> CheckStockAvailability(Cart cart)
        {
            var errors = new List<string>();

            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computer = await _context.Computers.FindAsync(item.ComputerId);
                    if (computer == null || computer.Quantity < item.Quantity)
                    {
                        errors.Add($"Недостаточно компьютеров '{item.Name}' в наличии");
                    }
                }
                else if (item.IsComponent)
                {
                    var component = await _context.Components.FindAsync(item.ComponentId);
                    if (component == null || component.Quantity < item.Quantity)
                    {
                        errors.Add($"Недостаточно комплектующих '{item.Name}' в наличии");
                    }
                }
            }

            return errors;
        }

        private async Task ReserveItems(Cart cart)
        {
            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computer = await _context.Computers.FindAsync(item.ComputerId);
                    if (computer != null)
                    {
                        computer.Quantity -= item.Quantity;
                        _context.Computers.Update(computer);
                    }
                }
                else if (item.IsComponent)
                {
                    var component = await _context.Components.FindAsync(item.ComponentId);
                    if (component != null)
                    {
                        component.Quantity -= item.Quantity;
                        _context.Components.Update(component);
                    }
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task UpdateUserInfo(int userId, string firstName, string lastName, string email, string phone)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                if (!string.IsNullOrEmpty(firstName)) user.FirstName = firstName;
                if (!string.IsNullOrEmpty(lastName)) user.LastName = lastName;
                if (!string.IsNullOrEmpty(email)) user.Email = email;
                if (!string.IsNullOrEmpty(phone)) user.Phone = phone;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
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
            // Для оффлайн заказов: списываем и создаем записи
            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computer = await _context.Computers.FindAsync(item.ComputerId);
                    if (computer != null)
                    {
                        computer.Quantity -= item.Quantity;
                        _context.Computers.Update(computer);

                        var computerOrder = new ComputerOrder
                        {
                            OrderId = orderId,
                            ComputerId = item.ComputerId,
                            Quantity = item.Quantity,
                            UnitPrice = item.Price
                        };
                        _context.ComputerOrders.Add(computerOrder);
                    }
                }
                else if (item.IsComponent)
                {
                    var component = await _context.Components.FindAsync(item.ComponentId);
                    if (component != null)
                    {
                        component.Quantity -= item.Quantity;
                        _context.Components.Update(component);

                        var componentOrder = new ComponentOrder
                        {
                            OrderId = orderId,
                            ComponentId = item.ComponentId,
                            Quantity = item.Quantity,
                            UnitPrice = item.Price
                        };
                        _context.ComponentOrders.Add(componentOrder);
                    }
                }
            }
        }

        private async Task CreateOrderItems(Cart cart, int orderId)
        {
            // Для онлайн заказов: только создаем записи (товары уже списаны)
            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    var computerOrder = new ComputerOrder
                    {
                        OrderId = orderId,
                        ComputerId = item.ComputerId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    };
                    _context.ComputerOrders.Add(computerOrder);
                }
                else if (item.IsComponent)
                {
                    var componentOrder = new ComponentOrder
                    {
                        OrderId = orderId,
                        ComponentId = item.ComponentId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    };
                    _context.ComponentOrders.Add(componentOrder);
                }
            }
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
    }
}