using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebShopV3.Models
{
    public class Favorite
    {
        [Key]
        public int Id { get; set; }

        public int? UserId { get; set; }

        [StringLength(100)]
        public string? GuestId { get; set; }

        [Required]
        [StringLength(20)]
        public string ProductType { get; set; } = "Computer"; // "Computer" или "Component"

        [Required]
        public int ProductId { get; set; }

        [Required]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastViewed { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [NotMapped]
        public bool IsGuestFavorite => !string.IsNullOrEmpty(GuestId);
    }
}