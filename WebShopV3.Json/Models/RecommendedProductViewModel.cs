namespace WebShopV3.Json.Models
{
    public class RecommendedProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string ProductType { get; set; } // "Computer" или "Component"
        public string? ComponentType { get; set; } // Только для комплектующих (CPU, GPU и т.д.)
        public string? Description { get; set; }
        public int Quantity { get; set; }
    }
}