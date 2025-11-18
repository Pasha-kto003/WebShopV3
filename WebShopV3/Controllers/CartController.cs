using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebShopV3.Models;

namespace ComputerShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string CartSessionKey = "Cart";

        public CartController(ApplicationDbContext context)
        {
            _context = context;
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

        public IActionResult Final()
        {
            return View();
        }

        // POST: Cart/AddToCart
        // POST: Cart/AddToCart - универсальный метод для компьютеров и компонентов
        // GET и POST для добавления в корзину
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> AddToCart(int? computerId, int? componentId, int quantity = 1)
        {
            Console.WriteLine($"=== AddToCart called ===");
            Console.WriteLine($"ComputerId: {computerId}, ComponentId: {componentId}, Quantity: {quantity}");

            if (!computerId.HasValue && !componentId.HasValue)
            {
                TempData["ErrorMessage"] = "Не указан товар для добавления";
                return RedirectToAction("Index", "Home");
            }

            // Получаем корзину
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            string productName = "";
            bool success = false;

            if (computerId.HasValue)
            {
                // Добавление компьютера
                var computer = await _context.Computers.FindAsync(computerId.Value);
                if (computer == null)
                {
                    TempData["ErrorMessage"] = "Компьютер не найден";
                    return RedirectToAction("Catalog", "Home");
                }

                if (computer.Quantity < quantity)
                {
                    TempData["ErrorMessage"] = "Недостаточно компьютеров в наличии";
                    return RedirectToAction("Catalog", "Home");
                }

                var existingItem = cart.Items.FirstOrDefault(x => x.ComputerId == computerId && x.IsComputer);

                if (existingItem != null)
                {
                    if (computer.Quantity < existingItem.Quantity + quantity)
                    {
                        TempData["ErrorMessage"] = "Недостаточно компьютеров в наличии";
                        return RedirectToAction("Catalog", "Home");
                    }
                    existingItem.Quantity += quantity;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        ComputerId = computerId.Value,
                        Name = computer.Name,
                        Price = computer.Price,
                        Quantity = quantity,
                        ImageUrl = computer.ImageUrl
                    });
                }
                productName = computer.Name;
                success = true;
            }
            else if (componentId.HasValue)
            {
                // Добавление компонента
                var component = await _context.Components.FindAsync(componentId.Value);
                if (component == null)
                {
                    TempData["ErrorMessage"] = "Комплектующее не найдено";
                    return RedirectToAction("Catalog", "Home");
                }

                if (component.Quantity < quantity)
                {
                    TempData["ErrorMessage"] = "Недостаточно комплектующих в наличии";
                    return RedirectToAction("Catalog", "Home");
                }

                var existingItem = cart.Items.FirstOrDefault(x => x.ComponentId == componentId && x.IsComponent);

                if (existingItem != null)
                {
                    if (component.Quantity < existingItem.Quantity + quantity)
                    {
                        TempData["ErrorMessage"] = "Недостаточно комплектующих в наличии";
                        return RedirectToAction("Catalog", "Home");
                    }
                    existingItem.Quantity += quantity;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        ComponentId = componentId.Value,
                        Name = component.Name,
                        Price = component.Price,
                        Quantity = quantity,
                        ImageUrl = "default-component.jpg"
                    });
                }
                productName = component.Name;
                success = true;
            }

            if (success)
            {
                // Сохраняем корзину
                var updatedCartJson = JsonSerializer.Serialize(cart);
                HttpContext.Session.SetString(CartSessionKey, updatedCartJson);

                TempData["SuccessMessage"] = $"\"{productName}\" добавлен в корзину!";
            }

            // Возвращаем обратно на страницу, откуда пришли
            return RedirectToAction("Catalog", "Home");
        }


        // POST: Cart/UpdateQuantity
        // POST: Cart/UpdateQuantity - универсальный метод
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int? computerId, int? componentId, int quantity)
        {
            if (quantity <= 0)
            {
                return RemoveFromCart(computerId, componentId);
            }

            object itemToCheck = null;
            if (computerId.HasValue)
            {
                itemToCheck = await _context.Computers.FindAsync(computerId.Value);
            }
            else if (componentId.HasValue)
            {
                itemToCheck = await _context.Components.FindAsync(componentId.Value);
            }

            if (itemToCheck == null)
            {
                return Json(new { success = false, message = "Товар не найден" });
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
                    if (computer.Quantity < quantity)
                    {
                        return Json(new { success = false, message = "Недостаточно товара в наличии" });
                    }
                }
                else if (componentId.HasValue)
                {
                    var component = await _context.Components.FindAsync(componentId.Value);
                    if (component.Quantity < quantity)
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

        // POST: Cart/RemoveFromCart - универсальный метод
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

            // Проверяем наличие всех товаров в корзине
            foreach (var item in cart.Items)
            {
                if (item.IsComputer)
                {
                    // Проверяем компьютер
                    var computer = await _context.Computers.FindAsync(item.ComputerId);
                    if (computer == null)
                    {
                        TempData["ErrorMessage"] = $"Компьютер '{item.Name}' не найден";
                        return RedirectToAction("Index");
                    }
                    if (computer.Quantity < item.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Недостаточно компьютеров '{item.Name}' в наличии. Доступно: {computer.Quantity}, Заказано: {item.Quantity}";
                        return RedirectToAction("Index");
                    }
                }
                else if (item.IsComponent)
                {
                    // Проверяем компонент
                    var component = await _context.Components.FindAsync(item.ComponentId);
                    if (component == null)
                    {
                        TempData["ErrorMessage"] = $"Комплектующее '{item.Name}' не найдено";
                        return RedirectToAction("Index");
                    }
                    if (component.Quantity < item.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Недостаточно комплектующих '{item.Name}' в наличии. Доступно: {component.Quantity}, Заказано: {item.Quantity}";
                        return RedirectToAction("Index");
                    }
                }
            }

            // Получаем данные текущего пользователя
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            var user = await _context.Users
                .Include(u => u.UserType)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Пользователь не найден";
                return RedirectToAction("Index");
            }

            ViewBag.UserData = new
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone
            };

            return View(cart);
        }

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
                // Получаем ID текущего пользователя
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                // ПРЕДВАРИТЕЛЬНАЯ ПРОВЕРКА НАЛИЧИЯ ТОВАРОВ
                foreach (var item in cart.Items)
                {
                    if (item.IsComputer)
                    {
                        var computer = await _context.Computers.FindAsync(item.ComputerId);
                        if (computer == null || computer.Quantity < item.Quantity)
                        {
                            throw new Exception($"Недостаточно компьютеров '{item.Name}' в наличии. Доступно: {computer?.Quantity ?? 0}, Заказано: {item.Quantity}");
                        }
                    }
                    else if (item.IsComponent)
                    {
                        var component = await _context.Components.FindAsync(item.ComponentId);
                        if (component == null || component.Quantity < item.Quantity)
                        {
                            throw new Exception($"Недостаточно комплектующих '{item.Name}' в наличии. Доступно: {component?.Quantity ?? 0}, Заказано: {item.Quantity}");
                        }
                    }
                }

                // Обновляем данные пользователя
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    if (!string.IsNullOrEmpty(firstName)) user.FirstName = firstName;
                    if (!string.IsNullOrEmpty(lastName)) user.LastName = lastName;
                    if (!string.IsNullOrEmpty(email)) user.Email = email;
                    if (!string.IsNullOrEmpty(phone)) user.Phone = phone;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync(); // Сохраняем изменения пользователя
                }

                // Создаем заказ
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.Now,
                    OrderTypeId = 3, // Продажа
                    StatusId = 5, // В ожидании
                    TotalAmount = cart.TotalAmount,
                    Description = $"Адрес доставки: {address}. {(string.IsNullOrEmpty(comment) ? "" : $"Комментарий: {comment}")}"
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Сохраняем заказ чтобы получить ID

                Console.WriteLine($"Order created with ID: {order.Id}");

                // Обрабатываем товары в корзине
                foreach (var item in cart.Items)
                {
                    if (item.IsComputer)
                    {
                        // Обработка компьютера
                        var computer = await _context.Computers.FindAsync(item.ComputerId);
                        if (computer != null)
                        {
                            // Проверяем наличие еще раз перед списанием
                            if (computer.Quantity < item.Quantity)
                            {
                                throw new Exception($"Недостаточно компьютеров '{computer.Name}' в наличии при обработке заказа");
                            }

                            computer.Quantity -= item.Quantity;
                            _context.Computers.Update(computer);

                            var computerOrder = new ComputerOrder
                            {
                                OrderId = order.Id,
                                ComputerId = item.ComputerId,
                                Quantity = item.Quantity,
                                UnitPrice = item.Price
                            };

                            _context.ComputerOrders.Add(computerOrder);
                        }
                    }
                    else if (item.IsComponent)
                    {
                        // Обработка компонента
                        var component = await _context.Components.FindAsync(item.ComponentId);
                        if (component != null)
                        {
                            // Проверяем наличие еще раз перед списанием
                            if (component.Quantity < item.Quantity)
                            {
                                throw new Exception($"Недостаточно комплектующих '{component.Name}' в наличии при обработке заказа");
                            }

                            component.Quantity -= item.Quantity;
                            _context.Components.Update(component);

                            // ИСПРАВЛЕННАЯ ЧАСТЬ: не устанавливаем навигационные свойства вручную
                            var componentOrder = new ComponentOrder
                            {
                                OrderId = order.Id,
                                ComponentId = item.ComponentId,
                                Quantity = item.Quantity,
                                UnitPrice = item.Price
                            };

                            _context.ComponentOrders.Add(componentOrder);
                        }
                    }
                }

                await _context.SaveChangesAsync(); // Сохраняем все изменения

                // Очищаем корзину
                HttpContext.Session.Remove(CartSessionKey);

                TempData["SuccessMessage"] = $"Заказ #{order.Id} успешно оформлен! Сумма: {order.TotalAmount:C}";
                return RedirectToAction("MyOrders", "Order");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при оформлении заказа: {ex.Message}";
                return RedirectToAction("Checkout");
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