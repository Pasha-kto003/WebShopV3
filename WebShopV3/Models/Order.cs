using System.ComponentModel.DataAnnotations;

namespace WebShopV3.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime OrderDate { get; set; }

        [Required]
        public decimal TotalAmount { get; set; }

        public int UserId { get; set; }
        public int OrderTypeId { get; set; }
        public int StatusId { get; set; }

        // Навигационные свойства
        public virtual User User { get; set; }
        public virtual OrderType OrderType { get; set; }
        public virtual Status Status { get; set; }
        public virtual ICollection<ComputerOrder> ComputerOrders { get; set; }

        public Order()
        {
            ComputerOrders = new HashSet<ComputerOrder>();
        }
    }
}
