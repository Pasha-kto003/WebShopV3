// Middleware/RecommendationTrackingMiddleware.cs
using WebShopV3.Models.Recommendation;
using WebShopV3.Services;

namespace WebShopV3.Middleware
{
    public class RecommendationTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RecommendationTrackingMiddleware> _logger;

        public RecommendationTrackingMiddleware(
            RequestDelegate next,
            ILogger<RecommendationTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IRecommendationService recommendationService)
        {
            // Пропускаем статические файлы и API запросы
            if (context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Path.StartsWithSegments("/lib") ||
                context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js") ||
                context.Request.Path.StartsWithSegments("/images"))
            {
                await _next(context);
                return;
            }

            try
            {
                // Отслеживаем просмотр страниц товаров
                if (context.Request.Path.StartsWithSegments("/Home/ComputerDetails") ||
                    context.Request.Path.StartsWithSegments("/Home/ComponentDetails"))
                {
                    await TrackProductViewAsync(context, recommendationService);
                }

                // Отслеживаем добавление в корзину (если есть параметры)
                if (context.Request.Path.StartsWithSegments("/Cart/AddToCart") &&
                    context.Request.Method == "POST")
                {
                    await TrackAddToCartAsync(context, recommendationService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in recommendation tracking middleware");
            }

            await _next(context);
        }

        private async Task TrackProductViewAsync(HttpContext context, IRecommendationService recommendationService)
        {
            try
            {
                var routeData = context.GetRouteData();
                var productId = routeData?.Values["id"]?.ToString();

                if (int.TryParse(productId, out int id))
                {
                    var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var guestId = context.Request.Cookies["GuestId"] ?? context.Session.Id;

                    var isComputer = context.Request.Path.StartsWithSegments("/Home/ComputerDetails");
                    var productType = isComputer ? "Computer" : "Component";

                    var action = new UserAction
                    {
                        UserId = userId != null ? int.Parse(userId) : (int?)null,
                        GuestId = guestId,
                        ActionType = UserActionType.View,
                        ProductType = productType,
                        ProductId = id,
                        Timestamp = DateTime.UtcNow
                    };

                    recommendationService.TrackAction(action);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking product view");
            }
        }

        private async Task TrackAddToCartAsync(HttpContext context, IRecommendationService recommendationService)
        {
            try
            {
                if (context.Request.HasFormContentType)
                {
                    var form = await context.Request.ReadFormAsync();
                    var computerId = form["computerId"].ToString();
                    var componentId = form["componentId"].ToString();

                    if (!string.IsNullOrEmpty(computerId) || !string.IsNullOrEmpty(componentId))
                    {
                        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var guestId = context.Request.Cookies["GuestId"] ?? context.Session.Id;

                        var action = new UserAction
                        {
                            UserId = userId != null ? int.Parse(userId) : (int?)null,
                            GuestId = guestId,
                            ActionType = UserActionType.AddToCart,
                            ProductType = !string.IsNullOrEmpty(computerId) ? "Computer" : "Component",
                            ProductId = !string.IsNullOrEmpty(computerId) ? int.Parse(computerId) : int.Parse(componentId),
                            Timestamp = DateTime.UtcNow
                        };

                        recommendationService.TrackAction(action);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking add to cart");
            }
        }
    }

    public static class RecommendationTrackingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRecommendationTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RecommendationTrackingMiddleware>();
        }
    }
}