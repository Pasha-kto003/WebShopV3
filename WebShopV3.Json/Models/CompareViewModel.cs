// Models/CompareViewModel.cs
using System.Collections.Generic;

namespace WebShopV3.Json.Models
{
    public class CompareViewModel
    {
        public List<CompareItem> Items { get; set; } = new List<CompareItem>();
        public List<string> AllSpecs { get; set; } = new List<string>();
        public int MaxItems { get; set; } = 4;
        public int CurrentCount => Items.Count;
        public bool CanAddMore => CurrentCount < MaxItems;
    }

    public class CompareItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Computer" или "Component"
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string PriceFormatted => Price.ToString("C");
        public string? Description { get; set; }
        public Dictionary<string, string> Specifications { get; set; } = new Dictionary<string, string>();
        public string Url { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string StockStatus => Quantity > 10 ? "В наличии" : Quantity > 3 ? "Мало" : "Заканчивается";
        public string StockClass => Quantity > 10 ? "stock-high" : Quantity > 3 ? "stock-medium" : "stock-low";
        public bool IsComputer => Type == "Computer";
        public bool IsComponent => Type == "Component";
    }
}