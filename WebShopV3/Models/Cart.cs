namespace WebShopV3.Models
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    public class CartItem
    {
        public int ComputerId { get; set; }
        public int ComponentId { get; set; }

        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
        public decimal TotalPrice => Price * Quantity;
        
        // Флаг для определения типа товара
        public bool IsComputer => ComputerId > 0;
        public bool IsComponent => ComponentId > 0;
    }

    public class Cart
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();
        public decimal TotalAmount => Items.Sum(x => x.TotalPrice);
        public int TotalItems => Items.Sum(x => x.Quantity);

        // Для компонентов
        public int TotalComponents => Items.Count(i => !i.IsComputer);
        // Для компьютеров
        public int TotalComputers => Items.Count(i => i.IsComputer);
    }
}

