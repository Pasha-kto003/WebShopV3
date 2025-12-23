// Models/Recommendation/RecommendationModels.cs
namespace WebShopV3.Models.Recommendation
{
    public enum UserActionType
    {
        View,
        AddToCart,
        Purchase,
        AddToFavorite,
        Compare,
        Search
    }

    public class UserAction
    {
        public int? UserId { get; set; }
        public string? GuestId { get; set; }
        public UserActionType ActionType { get; set; }
        public string ProductType { get; set; } // "Computer" или "Component"
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Category { get; set; } // Для компонентов: CPU, GPU, RAM и т.д.
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double Weight => GetActionWeight(ActionType);

        private double GetActionWeight(UserActionType actionType) => actionType switch
        {
            UserActionType.Purchase => 2.0,
            UserActionType.AddToCart => 1.5,
            UserActionType.AddToFavorite => 1.2,
            UserActionType.Compare => 1.0,
            UserActionType.View => 0.7,
            UserActionType.Search => 0.5,
            _ => 0.5
        };
    }

    public class RecommendationItem
    {
        public int Id { get; set; }
        public string ProductType { get; set; } // "Computer" или "Component"
        public string? ComponentType { get; set; } // Для компонентов
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string? Description { get; set; }
        public int StockQuantity { get; set; }
        public double RelevanceScore { get; set; } // 0-1
        public string RecommendationType { get; set; } // "similar", "complementary", "popular", "personalized"

        // Для отладки
        public Dictionary<string, double> ScoreFactors { get; set; } = new();
    }

    public class RecommendationRequest
    {
        public int? UserId { get; set; }
        public string? GuestId { get; set; }
        public int? CurrentProductId { get; set; }
        public string? CurrentProductType { get; set; }
        public string? CurrentComponentType { get; set; }
        public int Limit { get; set; } = 6;
        public bool IncludeComputers { get; set; } = true;
        public bool IncludeComponents { get; set; } = true;

        // Фильтры
        public decimal? MaxPrice { get; set; }
        public List<string>? ExcludeTypes { get; set; }
    }
}