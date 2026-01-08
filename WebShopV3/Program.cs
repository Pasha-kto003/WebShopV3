using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Net;
using WebShopV3.Middleware;
using WebShopV3.Models;
using WebShopV3.Services;

var builder = WebApplication.CreateBuilder(args);

// 🔒 Явно указываем Kestrel слушать только HTTP
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
});



// Services
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Настройка подключения к БД
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"Using connection string: {connectionString}");

// После получения строки подключения
var useInMemory = Environment.GetEnvironmentVariable("USE_IN_MEMORY_DB") == "true";

if (useInMemory)
{
    Console.WriteLine("Using InMemory database");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("ComputerShopDbV1"));
}
else
{
    Console.WriteLine($"Using SQL Server: {connectionString}");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            connectionString,
            sqlServerOptions =>
            {
                sqlServerOptions.EnableRetryOnFailure();
                sqlServerOptions.CommandTimeout(60);
            }));
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.None
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Админ"));
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Менеджер"));
    options.AddPolicy("AdminOrManager", policy => policy.RequireRole("Админ", "Менеджер"));
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("StrongPassword", policy =>
        policy.RequireClaim("PasswordStrength", "Strong"));
});

builder.Services.AddScoped<IPasswordHasher, PasswordHasherService>();
builder.Services.AddScoped<CompatibilityService>();
builder.Services.AddHttpClient<IYookassaService, YookassaService>();
builder.Services.AddScoped<IYookassaService, YookassaService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IComparisonService, ComparisonService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseRecommendationTracking();

// Инициализация БД
await InitializeDatabase(app);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "recommendations",
    pattern: "recommendations/{action=My}/{id?}",
    defaults: new { controller = "Recommendation" });

app.Run();

async Task InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        Console.WriteLine("Пытаемся подключиться к SQL Server...");

        // Пытаемся подключиться несколько раз
        for (int i = 1; i <= 20; i++)
        {
            try
            {
                Console.WriteLine($"Попытка подключения {i}/20...");

                if (await context.Database.CanConnectAsync())
                {
                    Console.WriteLine("Подключение к SQL Server успешно!");

                    // Создаем БД если не существует
                    await context.Database.EnsureCreatedAsync();
                    Console.WriteLine("База данных проверена/создана.");

                    // Инициализируем данные
                    DbInitializer.Initialize(context);
                    Console.WriteLine("База данных инициализирована.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка на попытке {i}: {ex.Message}");
            }

            await Task.Delay(3000); // Ждем 3 секунды
        }

        Console.WriteLine("Не удалось подключиться к SQL Server. Приложение будет работать без БД.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка инициализации БД: {ex.Message}");
    }
}