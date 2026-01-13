using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;
using Order = WebShopV3.Json.Models.Order;

namespace WebShopV3.Json.Controllers
{
    public class AuthController : Controller
    {
        private readonly JsonDataService _jsonData;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<AuthController> _logger;
        private readonly IMemoryCache _cache;
        private const int MAX_FAILED_ATTEMPTS = 5;
        private const int LOCKOUT_DURATION_MINUTES = 15;
        private const int DELAY_BASE_MS = 1000;

        public AuthController(
            JsonDataService jsonData,
            IPasswordHasher passwordHasher,
            ILogger<AuthController> logger,
            IMemoryCache cache)
        {
            _jsonData = jsonData;
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
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] bool rememberMe = false, string? returnUrl = null)
        {
            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("", "Имя пользователя и пароль обязательны");
                    await Task.Delay(DELAY_BASE_MS);
                    return View();
                }

                // Проверка блокировки по IP
                var clientIp = GetClientIp();
                if (IsIpLockedOut(clientIp))
                {
                    ModelState.AddModelError("", $"Слишком много неудачных попыток. Попробуйте позже.");
                    return View();
                }

                // Получаем пользователя из JSON
                var users = await _jsonData.GetAllAsync<User>("Users");
                var userTypes = await _jsonData.GetAllAsync<UserType>("UserTypes");

                var user = users.FirstOrDefault(u =>
                    u.Username == username || u.Email == username);

                if (user == null)
                {
                    // Увеличиваем счетчик неудачных попыток для IP
                    await RecordFailedAttemptAsync(clientIp, username);

                    _logger.LogWarning("Неудачная попытка входа для пользователя: {Username} с IP: {IP}",
                        username, clientIp);

                    await ProgressiveDelayAsync(clientIp);
                    ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                    return View();
                }

                // Добавляем UserType к пользователю
                user.UserType = userTypes.FirstOrDefault(ut => ut.Id == user.UserTypeId);

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
                    await RecordFailedAttemptAsync(clientIp, username, user.Id);
                    _logger.LogWarning("Неверный пароль для пользователя: {Username} (ID: {UserId}) с IP: {IP}",
                        username, user.Id, clientIp);
                    await ProgressiveDelayAsync(clientIp);
                    ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                    return View();
                }

                // Сбрасываем счетчик неудачных попыток при успешном входе
                ResetFailedAttempts(clientIp, user.Id);

                // Обновляем время последнего входа
                user.LastLoginAt = DateTime.Now;
                await _jsonData.UpdateAsync("Users", user);

                // Создаем claims
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.UserType?.Name ?? "Пользователь"),
            new Claim("UserType", user.UserType?.Name ?? "Пользователь"),
            new Claim("FullName", $"{user.FirstName} {user.LastName}".Trim()),
            new Claim("LastLogin", DateTime.Now.ToString("O")),
            // Добавляем хэш пароля для проверки при продлении сессии
            new Claim("PasswordHashCheck", _passwordHasher.GetPasswordHashCheck(password))
        };

                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                // Улучшенные настройки аутентификации
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe
                        ? DateTimeOffset.UtcNow.AddDays(30) // 30 дней для Remember Me
                        : DateTimeOffset.UtcNow.AddHours(12), // 12 часов без Remember Me
                    AllowRefresh = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    // Записываем в куки для клиентской проверки
                    RedirectUri = returnUrl
                };

                // Дополнительные настройки для куки
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    Expires = rememberMe
                        ? DateTimeOffset.UtcNow.AddDays(30)
                        : null, // Сессионная кука без Remember Me
                    MaxAge = rememberMe
                        ? TimeSpan.FromDays(30)
                        : null
                };

                // Добавляем кастомный claim для Remember Me
                if (rememberMe)
                {
                    claims.Add(new Claim("RememberMe", "true"));

                    // Генерируем и сохраняем токен Remember Me
                    var rememberToken = Guid.NewGuid().ToString();
                    var rememberTokenHash = _passwordHasher.HashPassword(rememberToken);

                    // Сохраняем токен в базе данных пользователя
                    user.RememberMeToken = rememberTokenHash;
                    user.RememberMeTokenExpires = DateTime.UtcNow.AddDays(30);
                    await _jsonData.UpdateAsync("Users", user);

                    // Добавляем claim с токеном
                    claims.Add(new Claim("RememberToken", rememberToken));
                }

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // Устанавливаем дополнительную куку для Remember Me
                if (rememberMe)
                {
                    Response.Cookies.Append(
                        "WebShop_RememberMe",
                        user.Id.ToString(),
                        new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Strict,
                            Expires = DateTimeOffset.UtcNow.AddDays(30),
                            MaxAge = TimeSpan.FromDays(30)
                        }
                    );
                }

                _logger.LogInformation("Успешный вход пользователя: {Username} (ID: {UserId}) с IP: {IP}. RememberMe: {RememberMe}",
                    user.Username, user.Id, clientIp, rememberMe);

                TempData["SuccessMessage"] = $"Добро пожаловать, {user.Username}!";

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                var guestCartJson = HttpContext.Session.GetString("Cart");
                if (!string.IsNullOrEmpty(guestCartJson))
                {
                    await MergeGuestCartWithUserCart(user.Id);
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

        // Вспомогательные методы безопасности (остаются без изменений)
        private string GetClientIp()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

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

        private async Task RecordFailedAttemptAsync(string clientIp, string username, int? userId = null)
        {
            var cacheKey = $"FailedAttempts_{clientIp}";
            var userCacheKey = userId.HasValue ? $"UserFailedAttempts_{userId}" : null;

            var ipAttempts = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES);
                return 0;
            });

            ipAttempts++;
            _cache.Set(cacheKey, ipAttempts, TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES));

            if (userId.HasValue)
            {
                var userAttempts = _cache.GetOrCreate(userCacheKey, entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES);
                    return 0;
                });

                userAttempts++;
                _cache.Set(userCacheKey, userAttempts, TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES));

                if (userAttempts >= MAX_FAILED_ATTEMPTS)
                {
                    _cache.Set($"UserLockout_{userId}", DateTime.UtcNow, TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES));
                    _logger.LogWarning("Пользователь {UserId} заблокирован на {Minutes} минут",
                        userId, LOCKOUT_DURATION_MINUTES);
                }
            }

            if (ipAttempts >= MAX_FAILED_ATTEMPTS)
            {
                _cache.Set($"Lockout_{clientIp}", DateTime.UtcNow, TimeSpan.FromMinutes(LOCKOUT_DURATION_MINUTES));
                _logger.LogWarning("IP {IP} заблокирован на {Minutes} минут",
                    clientIp, LOCKOUT_DURATION_MINUTES);

                await LogSecurityEventAsync("IP_LOCKOUT",
                    $"IP {clientIp} заблокирован после {ipAttempts} неудачных попыток",
                    clientIp, username);
            }
        }

        private void ResetFailedAttempts(string clientIp, int userId)
        {
            _cache.Remove($"FailedAttempts_{clientIp}");
            _cache.Remove($"UserFailedAttempts_{userId}");
            _cache.Remove($"Lockout_{clientIp}");
            _cache.Remove($"UserLockout_{userId}");
        }

        private bool IsIpLockedOut(string clientIp)
        {
            return _cache.TryGetValue($"Lockout_{clientIp}", out DateTime lockoutTime)
                && (DateTime.UtcNow - lockoutTime).TotalMinutes < LOCKOUT_DURATION_MINUTES;
        }

        private bool IsUserLockedOut(int userId)
        {
            return _cache.TryGetValue($"UserLockout_{userId}", out DateTime lockoutTime)
                && (DateTime.UtcNow - lockoutTime).TotalMinutes < LOCKOUT_DURATION_MINUTES;
        }

        private async Task ProgressiveDelayAsync(string clientIp)
        {
            var attempts = _cache.Get<int?>($"FailedAttempts_{clientIp}") ?? 0;
            var delayMultiplier = Math.Min(attempts, 10);
            var delayMs = DELAY_BASE_MS * delayMultiplier;
            await Task.Delay(delayMs);
        }

        private async Task LogSecurityEventAsync(string eventType, string description, string ip, string username = null)
        {
            try
            {
                _logger.LogInformation("Security Event: {EventType} - {Description} (IP: {IP}, User: {Username})",
                    eventType, description, ip, username ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при логировании события безопасности");
            }
        }

        private async Task MergeGuestCartWithUserCart(int userId)
        {
            try
            {
                var cartJson = HttpContext.Session.GetString("Cart");
                if (!string.IsNullOrEmpty(cartJson))
                {
                    HttpContext.Session.Remove("Cart");
                    _logger.LogInformation("Корзина гостя перенесена для пользователя {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переносе корзины для пользователя {UserId}", userId);
            }
        }

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
                var users = await _jsonData.GetAllAsync<User>("Users");
                if (users.Any(u => u.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Пользователь с таким именем уже существует");
                    return View(model);
                }

                // Проверка уникальности email
                if (users.Any(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
                    return View(model);
                }

                // Получаем тип пользователя "Пользователь"
                var userTypes = await _jsonData.GetAllAsync<UserType>("UserTypes");
                var userType = userTypes.FirstOrDefault(ut => ut.Name == "Пользователь");

                if (userType == null)
                {
                    // Создаем типы пользователей, если их нет
                    userType = new UserType { Id = 1, Name = "Пользователь" };
                    var userTypesList = new List<UserType>
                    {
                        new UserType { Id = 1, Name = "Админ" },
                        new UserType { Id = 2, Name = "Менеджер" },
                        new UserType { Id = 3, Name = "Пользователь" }
                    };
                    await _jsonData.SaveAllAsync("UserTypes", userTypesList);
                }

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

                await _jsonData.CreateAsync("Users", user);
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

            // Получаем пользователя
            var user = await _jsonData.GetByIdAsync<User>("Users", userId);
            if (user == null)
            {
                return NotFound();
            }

            // Получаем UserType
            var userTypes = await _jsonData.GetAllAsync<UserType>("UserTypes");
            user.UserType = userTypes.FirstOrDefault(ut => ut.Id == user.UserTypeId);

            // Получаем заказы пользователя
            var orders = await _jsonData.GetAllAsync<Order>("Orders");
            var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");
            var statuses = await _jsonData.GetAllAsync<Status>("Statuses");
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");

            // Фильтруем заказы пользователя
            user.Orders = orders
                .Where(o => o.UserId == userId)
                .Select(o =>
                {
                    o.User = user;
                    o.OrderType = orderTypes.FirstOrDefault(ot => ot.Id == o.OrderTypeId);
                    o.Status = statuses.FirstOrDefault(s => s.Id == o.StatusId);

                    // Получаем ComputerOrders (упрощенно)
                    var computerOrders = new List<ComputerOrder>();
                    o.ComputerOrders = computerOrders;

                    return o;
                })
                .ToList();

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