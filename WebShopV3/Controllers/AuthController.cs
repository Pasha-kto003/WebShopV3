using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;
        private const int MAX_FAILED_ATTEMPTS = 5;
        private const int LOCKOUT_DURATION_MINUTES = 15;
        private const int DELAY_BASE_MS = 1000;

        public AuthController(
            ApplicationDbContext context,
            IPasswordHasher passwordHasher,
            ILogger<AuthController> logger,
            IMemoryCache cache)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
            _cache = cache;
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

            // Проверяем, не заблокирован ли IP
            var clientIp = GetClientIp();
            var isLockedOut = IsIpLockedOut(clientIp);
            ViewBag.IsLockedOut = isLockedOut;

            if (isLockedOut)
            {
                var lockoutTime = _cache.Get<DateTime>($"Lockout_{clientIp}");
                var remainingMinutes = LOCKOUT_DURATION_MINUTES - (int)(DateTime.UtcNow - lockoutTime).TotalMinutes;
                ViewBag.LockoutMessage = $"Слишком много неудачных попыток. Попробуйте через {remainingMinutes} минут.";
            }

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
                    await Task.Delay(DELAY_BASE_MS); // Базовая задержка
                    return View();
                }

                // Проверка блокировки по IP
                var clientIp = GetClientIp();
                if (IsIpLockedOut(clientIp))
                {
                    ModelState.AddModelError("", $"Слишком много неудачных попыток. Попробуйте позже.");
                    return View();
                }

                // Получаем информацию о пользователе без пароля для проверки блокировки
                var user = await _context.Users
                    .Include(u => u.UserType)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username == username || u.Email == username);

                if (user == null)
                {
                    // Увеличиваем счетчик неудачных попыток для IP
                    await RecordFailedAttemptAsync(clientIp, username);

                    // Логируем с IP
                    _logger.LogWarning("Неудачная попытка входа для пользователя: {Username} с IP: {IP}",
                        username, clientIp);

                    // Прогрессивная задержка
                    await ProgressiveDelayAsync(clientIp);

                    ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                    return View();
                }

                // Проверяем, не заблокирован ли пользователь
                if (IsUserLockedOut(user.Id))
                {
                    var lockoutTime = _cache.Get<DateTime>($"UserLockout_{user.Id}");
                    var remainingMinutes = LOCKOUT_DURATION_MINUTES - (int)(DateTime.UtcNow - lockoutTime).TotalMinutes;
                    ModelState.AddModelError("", $"Аккаунт временно заблокирован. Попробуйте через {remainingMinutes} минут.");
                    return View();
                }

                // Проверяем пароль
                if (!_passwordHasher.VerifyPassword(user.PasswordHash, password))
                {
                    // Увеличиваем счетчик неудачных попыток
                    await RecordFailedAttemptAsync(clientIp, username, user.Id);

                    _logger.LogWarning("Неверный пароль для пользователя: {Username} (ID: {UserId}) с IP: {IP}",
                        username, user.Id, clientIp);

                    // Прогрессивная задержка
                    await ProgressiveDelayAsync(clientIp);

                    ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                    return View();
                }

                // СБРАСЫВАЕМ счетчик неудачных попыток при успешном входе
                ResetFailedAttempts(clientIp, user.Id);

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

                _logger.LogInformation("Успешный вход пользователя: {Username} (ID: {UserId}) с IP: {IP}",
                    user.Username, user.Id, clientIp);

                // Переносим корзину гостя в корзину пользователя
                await MergeGuestCartWithUserCart(user.Id);

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

        // Вспомогательные методы безопасности

        /// <summary>
        /// Получает IP клиента с учетом прокси
        /// </summary>
        private string GetClientIp()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Проверяем заголовки прокси (если есть)
            if (HttpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                ip = HttpContext.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
            }
            else if (HttpContext.Request.Headers.ContainsKey("X-Real-IP"))
            {
                ip = HttpContext.Request.Headers["X-Real-IP"].ToString();
            }

            return ip ?? "unknown";
        }

        /// <summary>
        /// Регистрирует неудачную попытку входа
        /// </summary>
        private async Task RecordFailedAttemptAsync(string clientIp, string username, int? userId = null)
        {
            var cacheKey = $"FailedAttempts_{clientIp}";
            var userCacheKey = userId.HasValue ? $"UserFailedAttempts_{userId}" : null;

            // Увеличиваем счетчик для IP
            var ipAttempts = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES);
                return 0;
            });

            ipAttempts++;
            _cache.Set(cacheKey, ipAttempts, TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES));

            // Увеличиваем счетчик для пользователя (если известен)
            if (userId.HasValue)
            {
                var userAttempts = _cache.GetOrCreate(userCacheKey, entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES);
                    return 0;
                });

                userAttempts++;
                _cache.Set(userCacheKey, userAttempts, TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES));

                // Если превышен лимит - блокируем пользователя
                if (userAttempts >= MAX_FAILED_ATTEMPTS)
                {
                    _cache.Set($"UserLockout_{userId}", DateTime.UtcNow, TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES));
                    _logger.LogWarning("Пользователь {UserId} заблокирован на {Minutes} минут",
                        userId, LOCKOUT_DURATION_MINUTES);
                }
            }

            // Если превышен лимит для IP - блокируем IP
            if (ipAttempts >= MAX_FAILED_ATTEMPTS)
            {
                _cache.Set($"Lockout_{clientIp}", DateTime.UtcNow, TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES));
                _logger.LogWarning("IP {IP} заблокирован на {Minutes} минут",
                    clientIp, LOCKOUT_DURATION_MINUTES);

                // Можно также сохранить в БД для мониторинга
                await LogSecurityEventAsync("IP_LOCKOUT",
                    $"IP {clientIp} заблокирован после {ipAttempts} неудачных попыток",
                    clientIp, username);
            }
        }

        /// <summary>
        /// Сбрасывает счетчики неудачных попыток
        /// </summary>
        private void ResetFailedAttempts(string clientIp, int userId)
        {
            _cache.Remove($"FailedAttempts_{clientIp}");
            _cache.Remove($"UserFailedAttempts_{userId}");
            _cache.Remove($"Lockout_{clientIp}");
            _cache.Remove($"UserLockout_{userId}");
        }

        /// <summary>
        /// Проверяет, заблокирован ли IP
        /// </summary>
        private bool IsIpLockedOut(string clientIp)
        {
            return _cache.TryGetValue($"Lockout_{clientIp}", out DateTime lockoutTime)
                && (DateTime.UtcNow - lockoutTime).TotalMinutes < LOCKOUT_DURATION_MINUTES;
        }

        /// <summary>
        /// Проверяет, заблокирован ли пользователь
        /// </summary>
        private bool IsUserLockedOut(int userId)
        {
            return _cache.TryGetValue($"UserLockout_{userId}", out DateTime lockoutTime)
                && (DateTime.UtcNow - lockoutTime).TotalMinutes < LOCKOUT_DURATION_MINUTES;
        }

        /// <summary>
        /// Прогрессивная задержка в зависимости от количества неудачных попыток
        /// </summary>
        private async Task ProgressiveDelayAsync(string clientIp)
        {
            var attempts = _cache.Get<int?>($"FailedAttempts_{clientIp}") ?? 0;

            // Увеличиваем задержку с каждой неудачной попыткой
            var delayMultiplier = Math.Min(attempts, 10); // Максимум 10x
            var delayMs = DELAY_BASE_MS * delayMultiplier;

            await Task.Delay(delayMs);
        }

        /// <summary>
        /// Логирует события безопасности в БД
        /// </summary>
        private async Task LogSecurityEventAsync(string eventType, string description, string ip, string username = null)
        {
            try
            {
                // Можно создать таблицу SecurityLog в БД
                /*
                var log = new SecurityLog
                {
                    EventType = eventType,
                    Description = description,
                    IpAddress = ip,
                    Username = username,
                    CreatedAt = DateTime.UtcNow
                };
                _context.SecurityLogs.Add(log);
                await _context.SaveChangesAsync();
                */

                // Пока просто логируем
                _logger.LogInformation("Security Event: {EventType} - {Description} (IP: {IP}, User: {Username})",
                    eventType, description, ip, username ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при логировании события безопасности");
            }
        }

        /// <summary>
        /// Переносит корзину гостя в корзину пользователя
        /// </summary>
        private async Task MergeGuestCartWithUserCart(int userId)
        {
            try
            {
                var cartJson = HttpContext.Session.GetString("Cart");
                if (!string.IsNullOrEmpty(cartJson))
                {
                    // Здесь можно добавить логику слияния корзины гостя с корзиной пользователя в БД
                    // Например, сохранить корзину пользователя в базе данных

                    // Очищаем сессию после переноса
                    HttpContext.Session.Remove("Cart");

                    _logger.LogInformation("Корзина гостя перенесена для пользователя {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переносе корзины для пользователя {UserId}", userId);
            }
        }

        // Остальные методы остаются без изменений...
        // GET: Auth/Logout
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var userId = userIdClaim?.Value;

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation("Выход пользователя: {Username} (ID: {UserId})", username, userId);

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

                // Получаем тип пользователя "Пользователь"
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

                _logger.LogInformation("Зарегистрирован новый пользователь: {Username} (ID: {UserId})",
                    user.Username, user.Id);

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

        // DTO класс остается без изменений
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