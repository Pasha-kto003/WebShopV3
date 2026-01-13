// Services/AuthService.cs
using System.Security.Claims;

namespace WebShopV3.Json.Services
{
    public class AuthService
    {
        public int GetCurrentUserId(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return 0;
        }
    }
}