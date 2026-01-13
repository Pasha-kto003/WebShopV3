using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Text.Json;
using WebShopV3.Json.Models;
using WebShopV3.Json.Services;

var builder = WebApplication.CreateBuilder(args);

// === Явная настройка Kestrel: ТОЛЬКО HTTP в Docker ===
if (builder.Environment.IsEnvironment("Docker"))
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        // Полностью очищаем все эндпоинты и слушаем ТОЛЬКО HTTP на 5002
        serverOptions.ListenAnyIP(5002, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2; // или Http1 только
            // НЕ вызываем UseHttps()
        });
    });
}
else
{
    // Для локальной разработки (Visual Studio / launchSettings.json)
    builder.WebHost.UseUrls("http://localhost:5002", "https://localhost:7263");
}

// === Services ===
builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var userIdClaim = context.Principal.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    var userService = context.HttpContext.RequestServices.GetRequiredService<JsonDataService>();
                    var users = await userService.GetAllAsync<User>("Users");
                    var user = users.FirstOrDefault(u => u.Id.ToString() == userIdClaim.Value);

                    if (user != null)
                    {
                        var rememberTokenClaim = context.Principal.FindFirst("RememberToken");
                        if (rememberTokenClaim != null)
                        {
                            var passwordHasher = context.HttpContext.RequestServices.GetRequiredService<IPasswordHasher>();
                            if (!passwordHasher.VerifyPassword(user.RememberMeToken, rememberTokenClaim.Value))
                            {
                                context.RejectPrincipal();
                                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            }
                        }
                    }
                }
            }
        };

        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsEnvironment("Docker") || builder.Environment.IsProduction()
            ? CookieSecurePolicy.None
            : CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "WebShopV3.Auth";
        options.Cookie.MaxAge = TimeSpan.FromDays(30);
    });

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Путь к данным
string dataPath = builder.Environment.IsEnvironment("Docker") || builder.Environment.IsProduction()
    ? "/app/Data/Json"
    : Path.Combine(builder.Environment.ContentRootPath, "Data", "Json");

builder.Services.AddSingleton(dataPath);
builder.Services.AddScoped<JsonDataService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasherService>();
builder.Services.AddScoped<CompatibilityService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IComparisonService, ComparisonService>();
builder.Services.AddScoped<IRecentlyViewedService, RecentlyViewedService>();
builder.Services.AddScoped<IComponentLinkService, ComponentLinkService>();

builder.Services.AddHttpClient<IYookassaService, YookassaService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// === Middleware pipeline ===
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS не нужен без HTTPS — можно закомментировать
    // app.UseHsts();
}

// HTTPS-редирект — ТОЛЬКО НЕ В DOCKER
if (!app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapFallbackToController("Index", "Home");

app.Run();