using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Models
{
    public class Computer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        public int Quantity { get; set; }

        public string ImageUrl { get; set; }

        // Навигационные свойства
        public virtual ICollection<ComputerComponent> ComputerComponents { get; set; }
        public virtual ICollection<ComputerOrder> ComputerOrders { get; set; }

        public Computer()
        {
            ComputerComponents = new HashSet<ComputerComponent>();
            ComputerOrders = new HashSet<ComputerOrder>();
        }
    }
}
