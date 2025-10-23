namespace WebShopV3.Models
{
    using System.Collections.Generic;
    using System.Linq;

    public class CartItem
    {
        public int ComputerId { get; set; }
        public string ComputerName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
        public decimal TotalPrice => Price * Quantity;
    }

    public class Cart
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();
        public decimal TotalAmount => Items.Sum(x => x.TotalPrice);
        public int TotalItems => Items.Sum(x => x.Quantity);
    }
}

