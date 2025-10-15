namespace WebShopV3.Models
{
    public class ComputerComponent
    {
        public int ComputerId { get; set; }
        public int ComponentId { get; set; }

        // Навигационные свойства
        public virtual Computer Computer { get; set; }
        public virtual Component Component { get; set; }
    }
}
