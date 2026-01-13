// Models/UserBehavior.cs
namespace WebShopV3.Json.Models
{
    public class UserBehavior
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int? UserId { get; set; }
        public string? GuestId { get; set; }
        public int ProductId { get; set; }
        public string ProductType { get; set; } = string.Empty; // "Computer" или "Component"
        public string Action { get; set; } = string.Empty; // "view", "add_to_cart", "purchase"
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public decimal? Price { get; set; }
        public string? Category { get; set; }
        public int? DurationSeconds { get; set; }
        public decimal? Weight { get; set; } // Вес события для алгоритма

        // УБРАЛИ навигационные свойства User и связи с БД
    }

    public class BehaviorBatch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<UserBehavior> Behaviors { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsProcessed { get; set; } = false;
        public DateTime? ProcessedAt { get; set; }
    }
}