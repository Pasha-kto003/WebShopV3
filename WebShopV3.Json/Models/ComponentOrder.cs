namespace WebShopV3.Json.Models
{
    public class ComponentOrder
    {
        public int OrderId { get; set; }
        public int ComponentId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Навигационные свойства
        public virtual Order Order { get; set; }
        public virtual Component Component { get; set; }
    }
}
