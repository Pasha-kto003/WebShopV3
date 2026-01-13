using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Json.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        [Display(Name = "Имя пользователя")]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; }

        [StringLength(100)]
        [Display(Name = "Имя")]
        public string? FirstName { get; set; }

        [StringLength(100)]
        [Display(Name = "Фамилия")]
        public string? LastName { get; set; }

        public string? RememberMeToken { get; set; }
        public DateTime? RememberMeTokenExpires { get; set; }

        [Phone]
        [Display(Name = "Телефон")]
        public string? Phone { get; set; }

        public int UserTypeId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }

        // Навигационные свойства
        public virtual UserType UserType { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
        // Навигационные свойства
        public virtual ICollection<Favorite> Favorites { get; set; } = new HashSet<Favorite>();

        public User()
        {
            Orders = new HashSet<Order>();
            Favorites = new HashSet<Favorite>();
        }
    }
}
