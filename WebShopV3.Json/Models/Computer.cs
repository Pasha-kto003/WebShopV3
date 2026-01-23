using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebShopV3.Json.Models
{
    public class Computer
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

        public string CreatedBy { get; set; }

        public string ImageUrl { get; set; }

        // НАВИГАЦИОННЫЕ СВОЙСТВА (заполняются при загрузке)
        public List<ComputerComponent> ComputerComponents { get; set; } = new();

        // Вспомогательное свойство для удобства
        [JsonIgnore]
        public List<Component> Components =>
            ComputerComponents?.Select(cc => cc.Component).Where(c => c != null).ToList() ?? new();

        // Дополнительные свойства для отображения в каталоге
        public string GetComponentTypes()
        {
            var types = Components
                .Select(c => c.Type)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();

            return string.Join(", ", types);
        }

        public decimal GetTotalComponentsPrice() =>
            Components.Sum(c => c.Price);

        public bool HasComponentType(string type) =>
            Components.Any(c => c.Type == type);
    }
}
