namespace WebShopV3.Models
{
    public class ComputerOrder
    {
        public int ComputerId { get; set; }
        public int OrderId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Навигационные свойства
        public virtual Computer Computer { get; set; }
        public virtual Order Order { get; set; }
    }
}
