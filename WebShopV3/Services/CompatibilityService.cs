using WebShopV3.Models;

namespace WebShopV3.Services
{
    public class CompatibilityService
    {
        private readonly ApplicationDbContext _context;

        public CompatibilityService(ApplicationDbContext context)
        {
            _context = context;
        }

        public CompatibilityResult CheckCompatibility(List<Component> selectedComponents)
        {
            var result = new CompatibilityResult();
            var motherboard = selectedComponents.FirstOrDefault(c => c.Type == "MB");

            if (motherboard == null)
            {
                result.IsCompatible = false;
                result.Errors.Add("Не выбрана материнская плата");
                return result;
            }

            // Проверка совместимости процессора
            var cpu = selectedComponents.FirstOrDefault(c => c.Type == "CPU");
            if (cpu != null)
            {
                if (cpu.Socket != motherboard.Socket)
                {
                    result.IsCompatible = false;
                    result.Errors.Add($"Процессор {cpu.Name} не совместим с материнской платой {motherboard.Name} (сокет {cpu.Socket} ≠ {motherboard.Socket})");
                }
            }

            // Проверка совместимости оперативной памяти
            var rams = selectedComponents.Where(c => c.Type == "RAM").ToList();
            if (rams.Any())
            {
                foreach (var ram in rams)
                {
                    if (ram.MemoryType != motherboard.MemoryType)
                    {
                        result.IsCompatible = false;
                        result.Errors.Add($"Оперативная память {ram.Name} не совместима с материнской платой {motherboard.Name} (тип памяти {ram.MemoryType} ≠ {motherboard.MemoryType})");
                    }
                }

                // Проверка количества слотов памяти
                if (rams.Count > motherboard.MemorySlots)
                {
                    result.IsCompatible = false;
                    result.Errors.Add($"Количество модулей памяти ({rams.Count}) превышает количество слотов на материнской плате ({motherboard.MemorySlots})");
                }

                // Проверка общего объема памяти
                var totalMemory = rams.Sum(ram =>
                    int.TryParse(ram.ComponentCharacteristics
                        .FirstOrDefault(cc => cc.Characteristic?.Name == "Объем памяти")?.Value, out var memory)
                        ? memory : 0);

                if (totalMemory > motherboard.MaxMemory)
                {
                    result.IsCompatible = false;
                    result.Errors.Add($"Общий объем памяти ({totalMemory}GB) превышает максимально поддерживаемый материнской платой ({motherboard.MaxMemory}GB)");
                }
            }

            // Проверка совместимости корпуса
            var computerCase = selectedComponents.FirstOrDefault(c => c.Type == "Case");
            if (computerCase != null)
            {
                if (!IsFormFactorCompatible(motherboard.FormFactor, computerCase.FormFactor))
                {
                    result.IsCompatible = false;
                    result.Errors.Add($"Корпус {computerCase.Name} не совместим с материнской платой {motherboard.Name} (форм-фактор {motherboard.FormFactor} не подходит для {computerCase.FormFactor})");
                }
            }

            // Проверка блока питания и видеокарты
            var gpu = selectedComponents.FirstOrDefault(c => c.Type == "GPU");
            var psu = selectedComponents.FirstOrDefault(c => c.Type == "PSU");

            if (gpu != null && psu != null)
            {
                if (!IsPowerCompatible(gpu, psu))
                {
                    result.IsCompatible = false;
                    result.Errors.Add($"Блок питания {psu.Name} может не обеспечить достаточную мощность для видеокарты {gpu.Name}");
                }
            }

            // Если ошибок нет - совместимо
            if (!result.Errors.Any())
            {
                result.IsCompatible = true;
                result.SuccessMessage = "Все компоненты совместимы!";
            }

            return result;
        }

        private bool IsFormFactorCompatible(string motherboardFormFactor, string caseFormFactor)
        {
            var compatibilityMatrix = new Dictionary<string, List<string>>
            {
                { "ATX", new List<string> { "ATX", "E-ATX" } },
                { "mATX", new List<string> { "ATX", "mATX" } },
                { "ITX", new List<string> { "ATX", "mATX", "ITX" } }
            };

            return compatibilityMatrix.ContainsKey(motherboardFormFactor) &&
                   compatibilityMatrix[motherboardFormFactor].Contains(caseFormFactor);
        }

        private bool IsPowerCompatible(Component gpu, Component psu)
        {
            var gpuPower = GetGPUPowerRequirement(gpu);
            var psuPower = GetPSUPower(psu);

            // Оставляем запас 20% от номинальной мощности
            return psuPower >= gpuPower * 1.2m;
        }

        private int GetGPUPowerRequirement(Component gpu)
        {
            // требования по мощности для разных видеокарт
            var powerRequirements = new Dictionary<string, int>
            {
                { "RTX 4090", 450 }, { "RTX 4080", 320 }, { "RTX 4070", 200 },
                { "RTX 3090", 350 }, { "RTX 3080", 320 }, { "RTX 3070", 220 },
                { "RX 7900", 355 }, { "RX 7800", 263 }, { "RX 7700", 200 }
            };

            foreach (var requirement in powerRequirements)
            {
                if (gpu.Name.Contains(requirement.Key))
                    return requirement.Value;
            }

            return 200;
        }

        private int GetPSUPower(Component psu)
        {
            var powerCharacteristic = psu.ComponentCharacteristics
                .FirstOrDefault(cc => cc.Characteristic?.Name == "Мощность");

            if (powerCharacteristic != null && int.TryParse(powerCharacteristic.Value, out var power))
                return power;

            // Если характеристика не найдена, пытаемся извлечь из названия
            var match = System.Text.RegularExpressions.Regex.Match(psu.Name, @"(\d+)\s*[Ww]");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var extractedPower))
                return extractedPower;

            return 500;
        }
    }

    public class CompatibilityResult
    {
        public bool IsCompatible { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string SuccessMessage { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
