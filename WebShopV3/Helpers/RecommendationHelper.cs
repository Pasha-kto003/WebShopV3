// Helpers/RecommendationHelper.cs
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
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
                var userId = GetUserId(context);
                var guestId = GetGuestId(context);

                var action = new UserAction
                {
                    UserId = userId,
                    GuestId = guestId,
                    ActionType = UserActionType.View,
                    ProductType = productType,
                    ProductId = productId,
                    ProductName = productName,
                    Category = category,
                    Timestamp = DateTime.UtcNow
                };

                await recommendationService.TrackActionAsync(action);
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
            int limit = 4)
        {
            try
            {
                var userId = GetUserId(context);
                var guestId = GetGuestId(context);

                var request = new RecommendationRequest
                {
                    UserId = userId,
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

        public static int? GetUserId(HttpContext context)
        {
            var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null && int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        public static string GetGuestId(HttpContext context)
        {
            var guestId = context.Request.Cookies["RecommendationGuestId"];
            if (string.IsNullOrEmpty(guestId))
            {
                guestId = Guid.NewGuid().ToString();
                context.Response.Cookies.Append("RecommendationGuestId", guestId, new CookieOptions
                {
                    Expires = DateTime.UtcNow.AddDays(30),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    IsEssential = true
                });
            }
            return guestId;
        }
    }
}