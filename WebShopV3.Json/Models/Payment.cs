// WebShopV3.Json.Models/Payment.cs
namespace WebShopV3.Json.Models
{
    public class Payment
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public bool Paid { get; set; }
        public decimal Amount { get; set; }
        public string ConfirmationUrl { get; set; } = string.Empty;
        public int? OrderId { get; set; } // опционально, если сохраняешь
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}