using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebShopV3.Json.Models
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
        public string? Description { get; set; }

        // Навигационные свойства - ДОБАВЛЯЕМ [JsonIgnore]
        [JsonIgnore]
        public virtual User User { get; set; }

        [JsonIgnore]
        public virtual OrderType OrderType { get; set; }

        [JsonIgnore]
        public virtual Status Status { get; set; }

        [JsonIgnore]
        public virtual ICollection<ComputerOrder> ComputerOrders { get; set; } = new HashSet<ComputerOrder>();

        [JsonIgnore]
        public virtual ICollection<ComponentOrder> ComponentOrders { get; set; } = new HashSet<ComponentOrder>();

        // ДОБАВЛЯЕМ свойства для сериализации
        [JsonPropertyName("statusName")]
        public string StatusName => Status?.Name ?? string.Empty;

        [JsonPropertyName("orderTypeName")]
        public string OrderTypeName => OrderType?.Name ?? string.Empty;

        [JsonPropertyName("userName")]
        public string UserName => User?.Username ?? string.Empty;

        public Order()
        {
            ComputerOrders = new HashSet<ComputerOrder>();
            ComponentOrders = new HashSet<ComponentOrder>();
        }
    }
}
