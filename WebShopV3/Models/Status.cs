using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Models
{
    public class Status
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } // Выполнен (Id=4), В ожидании (Id=5), Отмена (Id=6), Проблема с наличием (Id=7)

        // Навигационные свойства
        public virtual ICollection<Order> Orders { get; set; }

        public Status()
        {
            Orders = new HashSet<Order>();
        }
    }
}
