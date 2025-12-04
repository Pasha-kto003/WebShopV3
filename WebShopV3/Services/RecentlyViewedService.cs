namespace WebShopV3.Services
{
    // Services/RecentlyViewedService.cs
    using System.Text.Json;

    public interface IRecentlyViewedService
    {
        void AddComputer(int computerId, string computerName, decimal price, string? imageUrl);
        List<RecentlyViewedItem> GetRecentlyViewed(int maxItems = 5);
        void ClearHistory();
    }

    public class RecentlyViewedService : IRecentlyViewedService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string CookieName = "RecentlyViewedComputers";
        private const int MaxItems = 10; // Максимум в куках
        private const int CookieExpireDays = 30;

        public RecentlyViewedService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void AddComputer(int computerId, string computerName, decimal price, string? imageUrl)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            // Получаем текущую историю
            var history = GetHistoryFromCookie();

            // Удаляем дубликаты (если уже есть этот компьютер)
            history.RemoveAll(x => x.ComputerId == computerId);

            // Добавляем новый просмотр в начало
            history.Insert(0, new RecentlyViewedItem
            {
                ComputerId = computerId,
                ComputerName = computerName,
                Price = price,
                ImageUrl = imageUrl,
                ViewedAt = DateTime.UtcNow
            });

            // Ограничиваем количество элементов
            if (history.Count > MaxItems)
            {
                history = history.Take(MaxItems).ToList();
            }

            // Сохраняем в куки
            SaveHistoryToCookie(history);
        }

        public List<RecentlyViewedItem> GetRecentlyViewed(int maxItems = 5)
        {
            var history = GetHistoryFromCookie();
            return history.Take(maxItems).ToList();
        }

        public void ClearHistory()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            httpContext.Response.Cookies.Delete(CookieName);
        }

        private List<RecentlyViewedItem> GetHistoryFromCookie()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return new List<RecentlyViewedItem>();

            var cookie = httpContext.Request.Cookies[CookieName];
            if (string.IsNullOrEmpty(cookie))
                return new List<RecentlyViewedItem>();

            try
            {
                return JsonSerializer.Deserialize<List<RecentlyViewedItem>>(cookie)
                    ?? new List<RecentlyViewedItem>();
            }
            catch
            {
                return new List<RecentlyViewedItem>();
            }
        }

        private void SaveHistoryToCookie(List<RecentlyViewedItem> history)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            var serialized = JsonSerializer.Serialize(history);

            var options = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(CookieExpireDays),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps // Только HTTPS в продакшене
            };

            httpContext.Response.Cookies.Append(CookieName, serialized, options);
        }
    }

    // Модель для хранения в куках
    public class RecentlyViewedItem
    {
        public int ComputerId { get; set; }
        public string ComputerName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime ViewedAt { get; set; }
    }
}
