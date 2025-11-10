using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Models
{
    public class Characteristic
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Название характеристики")]
        public string Name { get; set; }

        [StringLength(50)]
        [Display(Name = "Единица измерения")]
        public string Unit { get; set; }

        [StringLength(500)]
        [Display(Name = "Описание")]
        public string Description { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        // Навигационные свойства
        public virtual ICollection<ComponentCharacteristic> ComponentCharacteristics { get; set; }

        public Characteristic()
        {
            ComponentCharacteristics = new HashSet<ComponentCharacteristic>();
        }
    }
}