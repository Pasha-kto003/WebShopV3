// Models/FavoriteWithProduct.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace WebShopV3.Json.Models
{
    public class FavoriteWithProduct
    {
        public Favorite Favorite { get; set; }
        public Computer? Computer { get; set; }
        public Component? Component { get; set; }

        [NotMapped]
        public bool IsAvailable => (Computer?.Quantity ?? Component?.Quantity ?? 0) > 0;

        [NotMapped]
        public decimal Price => Computer?.Price ?? Component?.Price ?? 0;

        [NotMapped]
        public string Name => Computer?.Name ?? Component?.Name ?? "Товар не найден";

        [NotMapped]
        public string? ImageUrl => Computer?.ImageUrl ?? Component?.ImageUrl;

        [NotMapped]
        public string ProductType => Computer != null ? "Computer" : "Component";

        [NotMapped]
        public int ProductId => Computer?.Id ?? Component?.Id ?? 0;
    }

    public class FavoriteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public Favorite? Favorite { get; set; }
    }
}