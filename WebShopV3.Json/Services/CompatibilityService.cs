using System.Text.RegularExpressions;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;

namespace WebShopV3.Json.Services
{
    public class CompatibilityService
    {
        private readonly JsonDataService _jsonData;

        public CompatibilityService(JsonDataService jsonData)
        {
            _jsonData = jsonData;
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
                if (rams.Count > (motherboard.MemorySlots ?? 0))
                {
                    result.IsCompatible = false;
                    result.Errors.Add($"Количество модулей памяти ({rams.Count}) превышает количество слотов на материнской плате ({motherboard.MemorySlots})");
                }

                // Проверка общего объема памяти
                var totalMemory = rams.Sum(ram =>
                {
                    // Пытаемся получить объем памяти из характеристик
                    if (ram.ComponentCharacteristics != null)
                    {
                        var memoryChar = ram.ComponentCharacteristics
                            .FirstOrDefault(cc => cc.Characteristic?.Name == "Объем памяти");
                        if (memoryChar != null && int.TryParse(memoryChar.Value, out var memory))
                            return memory;
                    }

                    // Пытаемся извлечь из названия
                    var match = Regex.Match(ram.Name, @"(\d+)\s*[Gg][Bb]");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var extractedMemory))
                        return extractedMemory;

                    return 0;
                });

                if (totalMemory > (motherboard.MaxMemory ?? 0))
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

            // Проверка охлаждения процессора
            if (cpu != null)
            {
                var cooler = selectedComponents.FirstOrDefault(c => c.Type == "Cooler");
                if (cooler != null)
                {
                    if (!IsCoolerCompatible(cpu, cooler))
                    {
                        result.Warnings.Add($"Кулер {cooler.Name} может быть не совместим с процессором {cpu.Name}. Проверьте совместимость сокетов.");
                    }
                }
                else
                {
                    result.Warnings.Add("Не выбран кулер для процессора. Убедитесь, что процессор имеет штатное охлаждение или выберите отдельный кулер.");
                }
            }

            // Проверка количества портов SATA/M.2 для накопителей
            var storageDevices = selectedComponents.Where(c => c.Type == "SSD" || c.Type == "HDD").ToList();
            if (storageDevices.Any() && motherboard != null)
            {
                CheckStorageCompatibility(motherboard, storageDevices, result);
            }

            // Если ошибок нет - совместимо
            if (!result.Errors.Any())
            {
                result.IsCompatible = true;
                result.SuccessMessage = "Все компоненты совместимы!";

                // Добавляем рекомендации, если все хорошо
                AddRecommendations(selectedComponents, result);
            }

            return result;
        }

        private bool IsFormFactorCompatible(string motherboardFormFactor, string caseFormFactor)
        {
            if (string.IsNullOrEmpty(motherboardFormFactor) || string.IsNullOrEmpty(caseFormFactor))
                return true; // Если не указаны, считаем совместимыми

            var compatibilityMatrix = new Dictionary<string, List<string>>
            {
                // ATX (Standard ATX) - 305 × 244 мм
                { "ATX", new List<string> {
                    "ATX", "E-ATX", "XL-ATX", "Full-Tower", "Mid-Tower",
                    "Super-Tower", "Tower", "Extended-ATX"
                } },
    
                // Micro-ATX (mATX) - 244 × 244 мм
                { "Micro-ATX", new List<string> {
                    "ATX", "Micro-ATX", "mATX", "Mid-Tower", "Mini-Tower",
                    "Full-Tower", "Super-Tower", "Tower"
                } },
    
                // Mini-ITX - 170 × 170 мм
                { "Mini-ITX", new List<string> {
                    "ATX", "Micro-ATX", "mATX", "Mini-ITX", "ITX",
                    "Mid-Tower", "Mini-Tower", "Cube", "HTPC", "Desktop",
                    "Slim", "Small Form Factor", "SFF"
                } },
    
                // E-ATX (Extended ATX) - 305 × 330 мм
                { "E-ATX", new List<string> {
                    "E-ATX", "XL-ATX", "Full-Tower", "Super-Tower",
                    "Extended-ATX", "Tower"
                } },
    
                // XL-ATX - 345 × 262 мм
                { "XL-ATX", new List<string> {
                    "XL-ATX", "Full-Tower", "Super-Tower", "E-ATX"
                } },
    
                // SSI CEB - 305 × 267 мм
                { "SSI CEB", new List<string> {
                    "E-ATX", "Full-Tower", "Super-Tower", "Server Case"
                } },
    
                // SSI EEB - 330 × 305 мм
                { "SSI EEB", new List<string> {
                    "E-ATX", "Full-Tower", "Super-Tower", "Server Case",
                    "Workstation Case"
                } },
    
                // DTX - 203 × 244 мм
                { "DTX", new List<string> {
                    "ATX", "Micro-ATX", "Mini-ITX", "Mid-Tower", "Mini-Tower"
                } },
    
                // Flex-ATX - 229 × 191 мм
                { "Flex-ATX", new List<string> {
                    "ATX", "Micro-ATX", "Mini-ITX", "Mini-Tower"
                } },
    
                // HPTX - 345 × 381 мм (очень большой)
                { "HPTX", new List<string> {
                    "Full-Tower", "Super-Tower", "Server Case"
                } },
    
                // WTX - 356 × 425 мм (серверный)
                { "WTX", new List<string> {
                    "Server Case", "Workstation Case"
                } },
    
                // BTX - альтернативный стандарт
                { "BTX", new List<string> {
                    "BTX", "BTX Case"
                } },
    
                // Nano-ITX - 120 × 120 мм
                { "Nano-ITX", new List<string> {
                    "Mini-ITX", "HTPC", "Embedded", "NUC", "Small"
                } },
    
                // Pico-ITX - 100 × 72 мм
                { "Pico-ITX", new List<string> {
                    "Embedded", "Custom", "Mini"
                } },
    
                // Mobile-ITX - 75 × 45 мм
                { "Mobile-ITX", new List<string> {
                    "Embedded", "Industrial"
                } }
            };

            // Приводим к общему формату
            motherboardFormFactor = motherboardFormFactor.Trim().ToUpper();
            caseFormFactor = caseFormFactor.Trim().ToUpper();

            // Если форм-фактор не найден в матрице, считаем совместимым
            if (!compatibilityMatrix.ContainsKey(motherboardFormFactor))
                return true;

            return compatibilityMatrix[motherboardFormFactor]
                .Any(cf => cf.Trim().ToUpper() == caseFormFactor);
        }

        private bool IsPowerCompatible(Component gpu, Component psu)
        {
            var gpuPower = GetGPUPowerRequirement(gpu);
            var psuPower = GetPSUPower(psu);

            // Оставляем запас 20% от номинальной мощности
            return psuPower >= gpuPower * 1.2m;
        }

        private bool IsCoolerCompatible(Component cpu, Component cooler)
        {
            if (string.IsNullOrEmpty(cpu.Socket) || string.IsNullOrEmpty(cooler.Socket))
                return true; // Если сокет не указан, считаем совместимым

            // Проверяем, содержит ли строка сокета кулера сокет процессора
            var coolerSockets = cooler.Socket.Split(new[] { '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            return coolerSockets.Any(socket => socket.Trim().Equals(cpu.Socket.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private void CheckStorageCompatibility(Component motherboard, List<Component> storageDevices, CompatibilityResult result)
        {
            int m2Count = storageDevices.Count(s => s.FormFactor?.Contains("M.2") == true);
            int sataCount = storageDevices.Count(s => s.FormFactor?.Contains("2.5") == true || s.FormFactor?.Contains("3.5") == true);

            // Проверяем M.2 слоты
            if (m2Count > 0)
            {
                var m2Slots = GetM2SlotCount(motherboard);
                if (m2Count > m2Slots)
                {
                    result.Warnings.Add($"Количество M.2 накопителей ({m2Count}) превышает количество M.2 слотов на материнской плате ({m2Slots})");
                }
            }

            // Проверяем SATA порты
            if (sataCount > 0)
            {
                var sataPorts = GetSataPortCount(motherboard);
                if (sataCount > sataPorts)
                {
                    result.Warnings.Add($"Количество SATA накопителей ({sataCount}) превышает количество SATA портов на материнской плате ({sataPorts})");
                }
            }
        }

        private int GetM2SlotCount(Component motherboard)
        {
            // Пытаемся извлечь из характеристик
            if (motherboard.ComponentCharacteristics != null)
            {
                var m2Char = motherboard.ComponentCharacteristics
                    .FirstOrDefault(cc => cc.Characteristic?.Name == "Порты M.2");
                if (m2Char != null && int.TryParse(m2Char.Value, out var m2Slots))
                    return m2Slots;
            }

            // Значения по умолчанию в зависимости от чипсета
            var chipset = GetChipsetFromName(motherboard.Name);
            return chipset switch
            {
                string s when s.Contains("X670") || s.Contains("Z790") || s.Contains("Z690") => 3,
                string s when s.Contains("B650") || s.Contains("B760") || s.Contains("B660") => 2,
                string s when s.Contains("X570") || s.Contains("Z590") => 2,
                string s when s.Contains("B550") || s.Contains("B560") => 1,
                _ => 1
            };
        }

        private int GetSataPortCount(Component motherboard)
        {
            // Пытаемся извлечь из характеристик
            if (motherboard.ComponentCharacteristics != null)
            {
                var sataChar = motherboard.ComponentCharacteristics
                    .FirstOrDefault(cc => cc.Characteristic?.Name == "Порты SATA");
                if (sataChar != null && int.TryParse(sataChar.Value, out var sataPorts))
                    return sataPorts;
            }

            // Значения по умолчанию
            var chipset = GetChipsetFromName(motherboard.Name);
            return chipset switch
            {
                string s when s.Contains("X670") || s.Contains("Z790") || s.Contains("X570") => 8,
                string s when s.Contains("B650") || s.Contains("B760") || s.Contains("Z690") => 6,
                string s when s.Contains("B550") || s.Contains("B660") || s.Contains("Z590") => 4,
                _ => 4
            };
        }

        private string GetChipsetFromName(string name)
        {
            var match = Regex.Match(name, @"(B\d{3}|X\d{3}|Z\d{3}|A\d{3}|H\d{3})", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.ToUpper() : "";
        }

        private void AddRecommendations(List<Component> selectedComponents, CompatibilityResult result)
        {
            var recommendations = new List<string>();

            // Проверка наличия всех основных компонентов
            var componentTypes = selectedComponents.Select(c => c.Type).Distinct().ToList();

            if (!componentTypes.Contains("CPU"))
                recommendations.Add("Рекомендуем добавить процессор");

            if (!componentTypes.Contains("RAM"))
                recommendations.Add("Рекомендуем добавить оперативную память");

            if (!componentTypes.Contains("SSD") && !componentTypes.Contains("HDD"))
                recommendations.Add("Рекомендуем добавить накопитель (SSD или HDD)");

            if (!componentTypes.Contains("PSU"))
                recommendations.Add("Рекомендуем добавить блок питания");

            // Проверка на производительность
            var cpu = selectedComponents.FirstOrDefault(c => c.Type == "CPU");
            var gpu = selectedComponents.FirstOrDefault(c => c.Type == "GPU");

            if (cpu != null && gpu == null && IsHighEndCPU(cpu))
            {
                recommendations.Add("Для такого мощного процессора рекомендуется добавить дискретную видеокарту");
            }

            if (recommendations.Any())
            {
                result.Warnings.AddRange(recommendations);
            }
        }

        private bool IsHighEndCPU(Component cpu)
        {
            var cpuName = cpu.Name.ToUpper();
            return cpuName.Contains("I9") ||
                   cpuName.Contains("RYZEN 9") ||
                   cpuName.Contains("RYZEN 7") ||
                   cpuName.Contains("I7");
        }

        private int GetGPUPowerRequirement(Component gpu)
        {
            // требования по мощности для разных видеокарт
            var powerRequirements = new Dictionary<string, int>
            {
                { "RTX 4090", 450 }, { "RTX 4080", 320 }, { "RTX 4070", 200 },
                { "RTX 3090", 350 }, { "RTX 3080", 320 }, { "RTX 3070", 220 },
                { "RTX 3060", 170 }, { "RTX 3050", 130 },
                { "RX 7900", 355 }, { "RX 7800", 263 }, { "RX 7700", 200 },
                { "RX 7600", 165 }, { "RX 6600", 132 },
                { "GTX 1660", 120 }, { "GTX 1650", 75 },
                { "RTX A6000", 300 }, { "RTX A5000", 230 }
            };

            var gpuName = gpu.Name.ToUpper();
            foreach (var requirement in powerRequirements)
            {
                if (gpuName.Contains(requirement.Key.ToUpper()))
                    return requirement.Value;
            }

            // Пытаемся извлечь из характеристик
            if (gpu.ComponentCharacteristics != null)
            {
                var powerChar = gpu.ComponentCharacteristics
                    .FirstOrDefault(cc => cc.Characteristic?.Name == "Рекомендуемый БП");
                if (powerChar != null && int.TryParse(powerChar.Value, out var power))
                    return power;
            }

            // Значение по умолчанию
            return 200;
        }

        private int GetPSUPower(Component psu)
        {
            // Пытаемся извлечь из характеристик
            if (psu.ComponentCharacteristics != null)
            {
                var powerChar = psu.ComponentCharacteristics
                    .FirstOrDefault(cc => cc.Characteristic?.Name == "Мощность");
                if (powerChar != null && int.TryParse(powerChar.Value, out var power))
                    return power;
            }

            // Если характеристика не найдена, пытаемся извлечь из названия
            var match = Regex.Match(psu.Name, @"(\d+)\s*[Ww]");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var extractedPower))
                return extractedPower;

            // Значение по умолчанию
            return 500;
        }
    }

    public class CompatibilityResult
    {
        public bool IsCompatible { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string SuccessMessage { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();

        // Дополнительная информация о совместимости
        public Dictionary<string, string> CompatibilityDetails { get; set; } = new Dictionary<string, string>();
    }
}