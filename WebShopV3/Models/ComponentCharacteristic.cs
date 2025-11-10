using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Models
{
    public class ComponentCharacteristic
    {
        public int ComponentId { get; set; }
        public int CharacteristicId { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Значение")]
        public string Value { get; set; }

        // Навигационные свойства
        public virtual Component Component { get; set; }
        public virtual Characteristic Characteristic { get; set; }
    }
}