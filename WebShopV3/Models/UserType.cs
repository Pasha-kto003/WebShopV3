using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Models
{
    public class UserType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } // Админ, Пользователь, Менеджер

        // Навигационные свойства
        public virtual ICollection<User> Users { get; set; }

        public UserType()
        {
            Users = new HashSet<User>();
        }
    }
}
