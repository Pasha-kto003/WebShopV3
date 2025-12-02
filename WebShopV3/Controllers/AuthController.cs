using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using WebShopV3.Models;
using WebShopV3.Services;

namespace WebShopV3.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ApplicationDbContext context, IPasswordHasher passwordHasher,
            ILogger<AuthController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        // GET: Auth/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Auth/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            [FromForm] string username,
            [FromForm] string password,
            [FromForm] bool rememberMe = false,
            string? returnUrl = null)
        {
            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("", "Имя пользователя и пароль обязательны");
                    return View();
                }

                var user = await _context.Users
                    .Include(u => u.UserType)
                    .FirstOrDefaultAsync(u => u.Username == username || u.Email == username);

                if (user == null)
                {
                    // Логируем неудачную попытку входа
                    _logger.LogWarning("Неудачная попытка входа для пользователя: {Username}", username);

                    // Задержка для защиты от брутфорса
                    await Task.Delay(1000);

                    ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                    return View();
                }

                // Проверяем пароль
                if (!_passwordHasher.VerifyPassword(user.PasswordHash, password))
                {
                    _logger.LogWarning("Неверный пароль для пользователя: {Username}", username);
                    await Task.Delay(1000);

                    ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                    return View();
                }

                // Обновляем время последнего входа
                user.LastLoginAt = DateTime.Now;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Создаем claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.UserType.Name),
                    new Claim("UserType", user.UserType.Name),
                    new Claim("FullName", $"{user.FirstName} {user.LastName}".Trim())
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe
                        ? DateTimeOffset.UtcNow.AddDays(30)
                        : DateTimeOffset.UtcNow.AddHours(12),
                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("Успешный вход пользователя: {Username}", user.Username);

                TempData["SuccessMessage"] = $"Добро пожаловать, {user.Username}!";

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при входе пользователя: {Username}", username);
                ModelState.AddModelError("", "Произошла ошибка при входе. Попробуйте позже.");
                return View();
            }
        }

        // GET: Auth/Logout
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name;
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation("Выход пользователя: {Username}", username);

            TempData["SuccessMessage"] = "Вы вышли из системы";
            return RedirectToAction("Index", "Home");
        }

        // GET: Auth/Register
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Auth/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            [Bind("Username,Email,FirstName,LastName,Phone,Password")] UserRegistrationDto model)
        {
            try
            {

                // Проверка уникальности username
                if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Пользователь с таким именем уже существует");
                    return View(model);
                }

                // Проверка уникальности email
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
                    return View(model);
                }

                // Получаем тип пользователя "Пользователь" (должен быть создан в DbInitializer)
                var userType = await _context.UserTypes
                    .FirstOrDefaultAsync(ut => ut.Name == "Пользователь")
                    ?? throw new Exception("Тип пользователя не найден");

                // Создаем нового пользователя
                var user = new User
                {
                    Username = model.Username,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Phone = model.Phone,
                    PasswordHash = _passwordHasher.HashPassword(model.Password),
                    UserTypeId = userType.Id,
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Зарегистрирован новый пользователь: {Username}", user.Username);

                // Автоматически входим после регистрации
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, userType.Name),
                    new Claim("UserType", userType.Name)
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                TempData["SuccessMessage"] = "Регистрация прошла успешно!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при регистрации пользователя: {Email}", model.Email);
                ModelState.AddModelError("", "Произошла ошибка при регистрации. Попробуйте позже.");
                return View(model);
            }
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: Auth/Profile
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var user = await _context.Users
                .Include(u => u.UserType)
                .Include(u => u.Orders)
                    .ThenInclude(o => o.Status)
                .Include(u => u.Orders)
                    .ThenInclude(o => o.OrderType)
                .Include(u => u.Orders)
                    .ThenInclude(o => o.ComputerOrders)
                    .ThenInclude(co => co.Computer)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        public class UserRegistrationDto
        {
            [Required(ErrorMessage = "Имя пользователя обязательно")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "Имя пользователя должно быть от 3 до 100 символов")]
            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Можно использовать только буквы, цифры и подчеркивание")]
            public string Username { get; set; }

            [Required(ErrorMessage = "Email обязателен")]
            [EmailAddress(ErrorMessage = "Некорректный email адрес")]
            [StringLength(255)]
            public string Email { get; set; }

            [StringLength(100)]
            public string? FirstName { get; set; }

            [StringLength(100)]
            public string? LastName { get; set; }

            [StringLength(20)]
            [Phone(ErrorMessage = "Некорректный номер телефона")]
            public string? Phone { get; set; }

            [Required(ErrorMessage = "Пароль обязателен")]
            [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен быть минимум 8 символов")]
            [DataType(DataType.Password)]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
                ErrorMessage = "Пароль должен содержать минимум одну заглавную букву, одну строчную, одну цифру и один спецсимвол")]
            public string Password { get; set; }

            [Required(ErrorMessage = "Подтверждение пароля обязательно")]
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Пароли не совпадают")]
            public string ConfirmPassword { get; set; }
        }
    }
}
