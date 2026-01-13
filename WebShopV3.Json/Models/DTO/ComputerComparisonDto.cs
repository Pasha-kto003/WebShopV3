// Models/DTO/ComputerComparisonDto.cs
namespace WebShopV3.Json.Models.DTO
{
    public class ComputerComparisonDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        public List<ComponentComparisonDto> Components { get; set; } = new List<ComponentComparisonDto>();
        public Dictionary<string, int> ComponentScores { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, string> AllCharacteristics { get; set; } = new Dictionary<string, string>();

        public int TotalScore { get; set; }
        public bool IsBest { get; set; }
        public string PerformanceLevel
        {
            get
            {
                if (TotalScore > 2000) return "Экстрим";
                if (TotalScore > 1500) return "Высокая";
                if (TotalScore > 1000) return "Средняя";
                return "Базовая";
            }
        }

        // Для отображения в таблице
        public string CPUInfo => GetComponentInfo("CPU");
        public string GPUInfo => GetComponentInfo("GPU");
        public string RAMInfo => GetComponentInfo("RAM");
        public string StorageInfo => GetComponentInfo("SSD") ?? GetComponentInfo("HDD") ?? "Не указано";

        private string GetComponentInfo(string type)
        {
            var component = Components.FirstOrDefault(c => c.Type == type);
            return component != null ? $"{component.Name} ({component.PerformanceScore} баллов)" : "Не указано";
        }
    }
}