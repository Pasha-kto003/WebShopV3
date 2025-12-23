// Helpers/RecommendationHelper.cs
using WebShopV3.Models.Recommendation;
using WebShopV3.Services;

namespace WebShopV3.Helpers
{
    public static class RecommendationHelper
    {
        public static async Task TrackViewAsync(
            HttpContext context,
            IRecommendationService recommendationService,
            int productId,
            string productType,
            string? productName = null,
            string? category = null)
        {
            try
            {
                var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var guestId = context.Request.Cookies["GuestId"] ?? context.Session.Id;

                var action = new UserAction
                {
                    UserId = userId != null ? int.Parse(userId) : (int?)null,
                    GuestId = guestId,
                    ActionType = UserActionType.View,
                    ProductType = productType,
                    ProductId = productId,
                    ProductName = productName,
                    Category = category,
                    Timestamp = DateTime.UtcNow
                };

                recommendationService.TrackAction(action);
            }
            catch
            {
                // Игнорируем ошибки трекинга
            }
        }

        public static async Task<List<RecommendationItem>> GetSidebarRecommendationsAsync(
            HttpContext context,
            IRecommendationService recommendationService,
            int? currentProductId = null,
            string? currentProductType = null,
            int limit = 5)
        {
            try
            {
                var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var guestId = context.Request.Cookies["GuestId"] ?? context.Session.Id;

                var request = new RecommendationRequest
                {
                    UserId = userId != null ? int.Parse(userId) : (int?)null,
                    GuestId = guestId,
                    CurrentProductId = currentProductId,
                    CurrentProductType = currentProductType,
                    Limit = limit,
                    IncludeComputers = true,
                    IncludeComponents = true
                };

                return await recommendationService.GetRecommendationsAsync(request);
            }
            catch
            {
                return new List<RecommendationItem>();
            }
        }


    }
}