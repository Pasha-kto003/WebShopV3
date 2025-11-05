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
        [HttpPost]
        public async Task<IActionResult> AddToCart(int computerId, int quantity = 1)
        {
            var computer = await _context.Computers.FindAsync(computerId);
            if (computer == null)
            {
                return Json(new { success = false, message = "Компьютер не найден" });
            }

            if (computer.Quantity < quantity)
            {
                return Json(new { success = false, message = "Недостаточно товара в наличии" });
            }

            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            var existingItem = cart.Items.FirstOrDefault(x => x.ComputerId == computerId);

            if (existingItem != null)
            {
                if (computer.Quantity < existingItem.Quantity + quantity)
                {
                    return Json(new { success = false, message = "Недостаточно товара в наличии" });
                }
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ComputerId = computerId,
                    ComputerName = computer.Name,
                    Price = computer.Price,
                    Quantity = quantity,
                    ImageUrl = computer.ImageUrl
                });
            }

            var updatedCartJson = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CartSessionKey, updatedCartJson);

            return Json(new
            {
                success = true,
                message = "Товар добавлен в корзину",
                totalItems = cart.TotalItems
            });
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int computerId, int quantity)
        {
            if (quantity <= 0)
            {
                return RemoveFromCart(computerId);
            }

            var computer = await _context.Computers.FindAsync(computerId);
            if (computer == null)
            {
                return Json(new { success = false, message = "Компьютер не найден" });
            }

            if (computer.Quantity < quantity)
            {
                return Json(new { success = false, message = "Недостаточно товара в наличии" });
            }

            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            var item = cart.Items.FirstOrDefault(x => x.ComputerId == computerId);

            if (item != null)
            {
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
        public IActionResult RemoveFromCart(int computerId)
        {
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson) ?? new Cart();

            var item = cart.Items.FirstOrDefault(x => x.ComputerId == computerId);

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

            foreach (var item in cart.Items)
            {
                var computer = _context.Computers.Find(item.ComputerId);
                if (computer == null || computer.Quantity < item.Quantity)
                {
                    TempData["ErrorMessage"] = $"Товар '{item.ComputerName}' больше не доступен в нужном количестве";
                    return RedirectToAction("Index");
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

                // Создаем заказ
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.Now,
                    OrderTypeId = 1, // Продажа
                    StatusId = 2, // В ожидании
                    TotalAmount = cart.TotalAmount
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Добавляем товары в заказ
                foreach (var item in cart.Items)
                {
                    var computer = await _context.Computers.FindAsync(item.ComputerId);
                    if (computer != null)
                    {
                        // Проверяем наличие
                        if (computer.Quantity < item.Quantity)
                        {
                            throw new Exception($"Недостаточно товара '{computer.Name}' в наличии");
                        }

                        // Уменьшаем количество на складе
                        computer.Quantity -= item.Quantity;
                        _context.Computers.Update(computer);

                        // Добавляем в заказ
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

                await _context.SaveChangesAsync();

                // Очищаем корзину
                HttpContext.Session.Remove(CartSessionKey);

                TempData["SuccessMessage"] = $"Заказ #{order.Id} успешно оформлен! Сумма: {order.TotalAmount:C}";
                return RedirectToAction("MyOrders", "Order");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при оформлении заказа: {ex.Message}";
                return RedirectToAction("Index");
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