namespace WebShopV3.Models.DTO
{
    public class ComponentComparisonDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public Dictionary<string, string> Characteristics { get; set; } = new Dictionary<string, string>();
        public int PerformanceScore { get; set; } = 0;

        // Для CPU
        public int? CoreCount { get; set; }
        public decimal? ClockSpeed { get; set; } // GHz

        // Для GPU
        public int? VRAM { get; set; } // GB
        public string? GPUModel { get; set; }

        // Для RAM
        public int? MemorySize { get; set; } // GB
        public string? MemorySpeed { get; set; } // MHz

        // Для Storage
        public int? StorageCapacity { get; set; } // GB
        public string? StorageType { get; set; } // SSD, HDD

        // Для Motherboard
        public string? Socket { get; set; }
        public string? Chipset { get; set; }
    }
}