// Services/ComparisonService.cs
using Microsoft.EntityFrameworkCore;
using WebShopV3.Models;
using WebShopV3.Models.DTO;
using System.Text.RegularExpressions;

namespace WebShopV3.Services
{
    public interface IComparisonService
    {
        Task<List<ComputerComparisonDto>> AnalyzeComputers(List<Computer> computers);
        Task<ComputerComparisonDto> GetBestComputer(List<Computer> computers);
        Dictionary<string, (string bestValue, int bestComputerIndex)> GetBestSpecifications(List<ComputerComparisonDto> computers);
    }

    public class ComparisonService : IComparisonService
    {
        private readonly ApplicationDbContext _context;

        public ComparisonService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ComputerComparisonDto>> AnalyzeComputers(List<Computer> computers)
        {
            var result = new List<ComputerComparisonDto>();

            foreach (var computer in computers)
            {
                var dto = new ComputerComparisonDto
                {
                    Id = computer.Id,
                    Name = computer.Name,
                    Price = computer.Price,
                    Quantity = computer.Quantity,
                    ImageUrl = computer.ImageUrl,
                    Url = $"/Home/ComputerDetails/{computer.Id}",
                    TotalScore = 0
                };

                var components = await _context.ComputerComponents
                    .Where(cc => cc.ComputerId == computer.Id)
                    .Include(cc => cc.Component)
                        .ThenInclude(c => c.ComponentCharacteristics)
                            .ThenInclude(cc => cc.Characteristic)
                    .Select(cc => cc.Component)
                    .ToListAsync();

                foreach (var component in components)
                {
                    var componentDto = await AnalyzeComponent(component);
                    dto.Components.Add(componentDto);
                    dto.ComponentScores[component.Type] = componentDto.PerformanceScore;

                    foreach (var kvp in componentDto.Characteristics)
                    {
                        dto.AllCharacteristics[$"{component.Type} - {kvp.Key}"] = kvp.Value;
                    }
                }

                dto.TotalScore = CalculateTotalScore(dto);
                result.Add(dto);
            }

            return result;
        }

        private async Task<ComponentComparisonDto> AnalyzeComponent(Component component)
        {
            var dto = new ComponentComparisonDto
            {
                Id = component.Id,
                Name = component.Name,
                Type = component.Type ?? "Unknown",
                Price = component.Price,
                Characteristics = new Dictionary<string, string>()
            };


            foreach (var cc in component.ComponentCharacteristics)
            {
                var characteristic = cc.Characteristic;
                if (characteristic != null)
                {
                    dto.Characteristics[characteristic.Name] = cc.Value;
                }
            }

            ParseSpecifications(component.Specifications, dto);

            dto.PerformanceScore = CalculateComponentScore(dto);

            return dto;
        }

        private void ParseSpecifications(string? specifications, ComponentComparisonDto dto)
        {
            if (string.IsNullOrEmpty(specifications))
                return;

            var specText = specifications.ToLower();

            // Для CPU
            if (dto.Type == "CPU")
            {
                // Ищем количество ядер
                var coreMatch = Regex.Match(specText, @"(\d+)\s*(ядер|core)");
                if (coreMatch.Success && int.TryParse(coreMatch.Groups[1].Value, out int cores))
                    dto.CoreCount = cores;

                // Ищем тактовую частоту
                var clockMatch = Regex.Match(specText, @"(\d+\.?\d*)\s*(ghz|ггц)");
                if (clockMatch.Success && decimal.TryParse(clockMatch.Groups[1].Value, out decimal clock))
                    dto.ClockSpeed = clock;

                // Извлекаем модель
                var modelMatch = Regex.Match(specifications, @"(i[3579]|Ryzen\s*[3579]|Core\s*i[3579])", RegexOptions.IgnoreCase);
                if (modelMatch.Success)
                    dto.GPUModel = modelMatch.Value;
            }

            // Для GPU
            else if (dto.Type == "GPU")
            {
                // Ищем объем VRAM
                var vramMatch = Regex.Match(specText, @"(\d+)\s*(gb|гб)\s*v?ram");
                if (!vramMatch.Success)
                    vramMatch = Regex.Match(specText, @"(\d+)\s*gb");

                if (vramMatch.Success && int.TryParse(vramMatch.Groups[1].Value, out int vram))
                    dto.VRAM = vram;

                // Извлекаем модель GPU
                var gpuModelMatch = Regex.Match(specifications, @"(RTX\s*\d+|GTX\s*\d+|RX\s*\d+)", RegexOptions.IgnoreCase);
                if (gpuModelMatch.Success)
                    dto.GPUModel = gpuModelMatch.Value;
            }

            // Для RAM
            else if (dto.Type == "RAM")
            {
                // Ищем объем памяти
                var ramMatch = Regex.Match(specText, @"(\d+)\s*(gb|гб)");
                if (ramMatch.Success && int.TryParse(ramMatch.Groups[1].Value, out int ramSize))
                    dto.MemorySize = ramSize;

                // Ищем частоту
                var speedMatch = Regex.Match(specText, @"(\d+)\s*(mhz|мгц)");
                if (speedMatch.Success)
                    dto.MemorySpeed = speedMatch.Groups[1].Value + " MHz";
            }

            // Для SSD/HDD
            else if (dto.Type == "SSD" || dto.Type == "HDD")
            {
                // Ищем объем
                var storageMatch = Regex.Match(specText, @"(\d+)\s*(tb|gb|тб|гб)");
                if (storageMatch.Success)
                {
                    if (int.TryParse(storageMatch.Groups[1].Value, out int size))
                    {
                        var unit = storageMatch.Groups[2].Value.ToLower();
                        dto.StorageCapacity = unit.Contains("tb") || unit.Contains("тб") ? size * 1024 : size;
                    }
                }
                dto.StorageType = dto.Type;
            }
        }

        private int CalculateComponentScore(ComponentComparisonDto component)
        {
            int score = 0;

            switch (component.Type.ToUpper())
            {
                case "CPU":
                    score += (component.CoreCount ?? 0) * 100;
                    score += (int)((component.ClockSpeed ?? 0) * 50);

                    // Бонусы за модель
                    if (component.GPUModel?.Contains("i9") == true || component.GPUModel?.Contains("Ryzen 9") == true)
                        score += 300;
                    else if (component.GPUModel?.Contains("i7") == true || component.GPUModel?.Contains("Ryzen 7") == true)
                        score += 200;
                    else if (component.GPUModel?.Contains("i5") == true || component.GPUModel?.Contains("Ryzen 5") == true)
                        score += 100;
                    break;

                case "GPU":
                    score += (component.VRAM ?? 0) * 150;

                    // Бонусы за модель
                    if (component.GPUModel?.Contains("RTX 40") == true)
                        score += 500;
                    else if (component.GPUModel?.Contains("RTX 30") == true)
                        score += 400;
                    else if (component.GPUModel?.Contains("RTX 20") == true)
                        score += 300;
                    else if (component.GPUModel?.Contains("GTX") == true)
                        score += 200;
                    break;

                case "RAM":
                    score += (component.MemorySize ?? 0) * 10;

                    // Бонус за скорость
                    if (component.MemorySpeed != null)
                    {
                        if (component.MemorySpeed.Contains("3600") || component.MemorySpeed.Contains("3200"))
                            score += 50;
                        else if (component.MemorySpeed.Contains("3000") || component.MemorySpeed.Contains("2666"))
                            score += 30;
                    }
                    break;

                case "SSD":
                    score += (component.StorageCapacity ?? 0) / 100; // 1 балл за 100GB
                    score += 100; // Бонус за SSD
                    break;

                case "HDD":
                    score += (component.StorageCapacity ?? 0) / 200; // 0.5 балла за 100GB
                    break;
            }

            return score;
        }

        private int CalculateTotalScore(ComputerComparisonDto computer)
        {
            int totalScore = 0;

            // Суммируем баллы всех компонентов
            foreach (var component in computer.Components)
            {
                totalScore += component.PerformanceScore;
            }

            // Добавляем бонус за баланс системы
            if (computer.Components.Any(c => c.Type == "CPU") &&
                computer.Components.Any(c => c.Type == "GPU") &&
                computer.Components.Any(c => c.Type == "RAM") &&
                computer.Components.Any(c => c.Type == "SSD" || c.Type == "HDD"))
            {
                totalScore += 200; // Бонус за полную систему
            }

            // Штраф за высокую цену (чем дешевле при той же производительности, тем лучше)
            if (computer.Price > 0)
            {
                double pricePerfRatio = totalScore / (double)computer.Price;
                totalScore = (int)(totalScore * Math.Min(pricePerfRatio * 10, 1.5));
            }

            return totalScore;
        }

        public async Task<ComputerComparisonDto> GetBestComputer(List<Computer> computers)
        {
            var analyzedComputers = await AnalyzeComputers(computers);
            return analyzedComputers.OrderByDescending(c => c.TotalScore).FirstOrDefault();
        }

        public Dictionary<string, (string bestValue, int bestComputerIndex)> GetBestSpecifications(List<ComputerComparisonDto> computers)
        {
            var bestSpecs = new Dictionary<string, (string bestValue, int bestComputerIndex)>();

            if (!computers.Any())
                return bestSpecs;

            var allSpecs = new HashSet<string>();
            foreach (var computer in computers)
            {
                foreach (var spec in computer.AllCharacteristics.Keys)
                {
                    allSpecs.Add(spec);
                }
            }

            foreach (var spec in allSpecs)
            {
                string bestValue = "";
                int bestIndex = -1;
                double bestNumericValue = double.MinValue;

                for (int i = 0; i < computers.Count; i++)
                {
                    if (computers[i].AllCharacteristics.TryGetValue(spec, out var value))
                    {
                        if (double.TryParse(value, out double numericValue))
                        {
                            if (numericValue > bestNumericValue)
                            {
                                bestNumericValue = numericValue;
                                bestValue = value;
                                bestIndex = i;
                            }
                        }
                        else if (string.IsNullOrEmpty(bestValue) || IsBetterStringValue(value, bestValue, spec))
                        {
                            bestValue = value;
                            bestIndex = i;
                        }
                    }
                }

                if (bestIndex >= 0)
                {
                    bestSpecs[spec] = (bestValue, bestIndex);
                }
            }

            return bestSpecs;
        }

        private bool IsBetterStringValue(string newValue, string currentBest, string specName)
        {
            // Эвристики для определения лучшего строкового значения
            var newVal = newValue.ToLower();
            var current = currentBest.ToLower();

            if (specName.Contains("GPU") || specName.Contains("Видеокарта"))
            {
                // Для GPU: RTX > GTX > другие
                if (newVal.Contains("rtx") && !current.Contains("rtx")) return true;
                if (newVal.Contains("gtx") && !current.Contains("gtx") && !current.Contains("rtx")) return true;

                // Более высокие номера моделей лучше
                var newNum = ExtractModelNumber(newVal);
                var currentNum = ExtractModelNumber(current);
                return newNum > currentNum;
            }

            if (specName.Contains("CPU") || specName.Contains("Процессор"))
            {
                // i9 > i7 > i5
                if (newVal.Contains("i9") && !current.Contains("i9")) return true;
                if (newVal.Contains("i7") && !current.Contains("i9") && !current.Contains("i7")) return true;
                if (newVal.Contains("i5") && !current.Contains("i9") && !current.Contains("i7") && !current.Contains("i5")) return true;

                // Ryzen 9 > 7 > 5
                if (newVal.Contains("ryzen 9") && !current.Contains("ryzen 9")) return true;
                if (newVal.Contains("ryzen 7") && !current.Contains("ryzen 9") && !current.Contains("ryzen 7")) return true;
                if (newVal.Contains("ryzen 5") && !current.Contains("ryzen 9") && !current.Contains("ryzen 7") && !current.Contains("ryzen 5")) return true;
            }

            return false;
        }

        private int ExtractModelNumber(string text)
        {
            var match = Regex.Match(text, @"\d{3,4}");
            return match.Success ? int.Parse(match.Value) : 0;
        }
    }
}