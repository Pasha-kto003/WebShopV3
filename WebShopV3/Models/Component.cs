using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Models
{
    public class Component
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

        [StringLength(50)]
        public string Type { get; set; } // CPU, GPU, RAM, etc.

        public string Specifications { get; set; }

        // Навигационные свойства
        public virtual ICollection<ComputerComponent> ComputerComponents { get; set; }
        public virtual ICollection<ComponentCharacteristic> ComponentCharacteristics { get; set; }

        public Component()
        {
            ComputerComponents = new HashSet<ComputerComponent>();
            ComponentCharacteristics = new HashSet<ComponentCharacteristic>();
        }
    }
}
