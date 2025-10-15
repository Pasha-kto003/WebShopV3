using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Models
{
    public class OrderType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } // Продажа, Привоз

        // Навигационные свойства
        public virtual ICollection<Order> Orders { get; set; }

        public OrderType()
        {
            Orders = new HashSet<Order>();
        }
    }
}
