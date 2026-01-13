using System.Text.Json;
using WebShopV3.Json.Models;

namespace WebShopV3.Json.Services
{
    public interface IComponentLinkService
    {
        Task<List<int>> GetComputerComponentsAsync(int computerId);
        Task<List<int>> GetComponentComputersAsync(int componentId);
        Task<List<ComponentCharacteristic>> GetComponentCharacteristicsAsync(int componentId);
        Task<Dictionary<string, string>> GetComponentCharacteristicsDictionaryAsync(int componentId);
        Task<List<Component>> GetComputerComponentsDetailsAsync(int computerId);
        Task<ComputerDetails> GetComputerDetailsAsync(int computerId);
        Task<List<ComponentWithCharacteristics>> GetComponentsWithCharacteristicsAsync(List<int> componentIds);
    }

    public class ComponentLinkService : IComponentLinkService
    {
        private readonly JsonDataService _jsonData;
        private readonly ILogger<ComponentLinkService> _logger;

        public ComponentLinkService(JsonDataService jsonData, ILogger<ComponentLinkService> logger)
        {
            _jsonData = jsonData;
            _logger = logger;
        }

        // Получить список ID компонентов компьютера
        public async Task<List<int>> GetComputerComponentsAsync(int computerId)
        {
            try
            {
                var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
                return computerComponents
                    .Where(cc => cc.ComputerId == computerId)
                    .Select(cc => cc.ComponentId)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting components for computer {computerId}");
                return new List<int>();
            }
        }

        // Получить список ID компьютеров, содержащих компонент
        public async Task<List<int>> GetComponentComputersAsync(int componentId)
        {
            try
            {
                var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
                return computerComponents
                    .Where(cc => cc.ComponentId == componentId)
                    .Select(cc => cc.ComputerId)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting computers for component {componentId}");
                return new List<int>();
            }
        }

        // Получить характеристики компонента
        public async Task<List<ComponentCharacteristic>> GetComponentCharacteristicsAsync(int componentId)
        {
            try
            {
                var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");
                var characteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");

                return componentCharacteristics
                    .Where(cc => cc.ComponentId == componentId)
                    .Select(cc => new ComponentCharacteristic
                    {
                        ComponentId = cc.ComponentId,
                        CharacteristicId = cc.CharacteristicId,
                        Value = cc.Value,
                        Characteristic = characteristics.FirstOrDefault(c => c.Id == cc.CharacteristicId)
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting characteristics for component {componentId}");
                return new List<ComponentCharacteristic>();
            }
        }

        // Получить характеристики компонента в виде словаря
        public async Task<Dictionary<string, string>> GetComponentCharacteristicsDictionaryAsync(int componentId)
        {
            try
            {
                var characteristics = await GetComponentCharacteristicsAsync(componentId);
                return characteristics
                    .Where(cc => cc.Characteristic != null)
                    .ToDictionary(
                        cc => cc.Characteristic!.Name,
                        cc => $"{cc.Value} {cc.Characteristic.Unit}".Trim()
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting characteristics dictionary for component {componentId}");
                return new Dictionary<string, string>();
            }
        }

        // Получить детальную информацию о компонентах компьютера
        public async Task<List<Component>> GetComputerComponentsDetailsAsync(int computerId)
        {
            try
            {
                var componentIds = await GetComputerComponentsAsync(computerId);
                if (!componentIds.Any())
                    return new List<Component>();

                var components = await _jsonData.GetAllAsync<Component>("Components");
                return components
                    .Where(c => componentIds.Contains(c.Id))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting component details for computer {computerId}");
                return new List<Component>();
            }
        }

        // Получить полную информацию о компьютере с его компонентами
        public async Task<ComputerDetails> GetComputerDetailsAsync(int computerId)
        {
            try
            {
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var computer = computers.FirstOrDefault(c => c.Id == computerId);

                if (computer == null)
                    return null;

                var components = await GetComputerComponentsDetailsAsync(computerId);
                var componentsWithCharacteristics = await GetComponentsWithCharacteristicsAsync(
                    components.Select(c => c.Id).ToList()
                );

                // Группируем компоненты по типам
                var groupedComponents = components
                    .GroupBy(c => c.Type)
                    .ToDictionary(
                        g => g.Key ?? "Other",
                        g => g.ToList()
                    );

                return new ComputerDetails
                {
                    Computer = computer,
                    Components = components,
                    GroupedComponents = groupedComponents,
                    ComponentsWithCharacteristics = componentsWithCharacteristics,
                    TotalComponentsCount = components.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting computer details for {computerId}");
                return null;
            }
        }

        // Получить компоненты с их характеристиками
        public async Task<List<ComponentWithCharacteristics>> GetComponentsWithCharacteristicsAsync(List<int> componentIds)
        {
            try
            {
                var result = new List<ComponentWithCharacteristics>();
                var components = await _jsonData.GetAllAsync<Component>("Components");

                foreach (var componentId in componentIds)
                {
                    var component = components.FirstOrDefault(c => c.Id == componentId);
                    if (component != null)
                    {
                        var characteristics = await GetComponentCharacteristicsDictionaryAsync(componentId);

                        result.Add(new ComponentWithCharacteristics
                        {
                            Component = component,
                            Characteristics = characteristics
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting components with characteristics");
                return new List<ComponentWithCharacteristics>();
            }
        }

        // Получить совместимые компоненты для данного компонента
        public async Task<List<Component>> GetCompatibleComponentsAsync(int componentId, string componentType)
        {
            try
            {
                var components = await _jsonData.GetAllAsync<Component>("Components");

                // Фильтруем по совместимости в зависимости от типа компонента
                return componentType switch
                {
                    "CPU" => components.Where(c => c.Type == "MB" && c.Socket != null).ToList(),
                    "MB" => components.Where(c => c.Type == "CPU" || c.Type == "RAM" || c.Type == "GPU").ToList(),
                    "RAM" => components.Where(c => c.Type == "MB").ToList(),
                    "GPU" => components.Where(c => c.Type == "PSU" || c.Type == "MB").ToList(),
                    "PSU" => components.Where(c => c.Type == "GPU" || c.Type == "MB" || c.Type == "CPU").ToList(),
                    "Case" => components.Where(c => c.Type == "MB" || c.Type == "PSU" || c.Type == "Cooler").ToList(),
                    _ => components.Where(c => c.Type != componentType).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting compatible components for {componentId}");
                return new List<Component>();
            }
        }
    }

    // Модели для работы со связанными данными
    public class ComputerDetails
    {
        public Computer Computer { get; set; }
        public List<Component> Components { get; set; } = new();
        public Dictionary<string, List<Component>> GroupedComponents { get; set; } = new();
        public List<ComponentWithCharacteristics> ComponentsWithCharacteristics { get; set; } = new();
        public int TotalComponentsCount { get; set; }
        public decimal TotalComponentsPrice => Components.Sum(c => c.Price);
        public decimal ComputerToComponentsPriceRatio =>
            Computer?.Price > 0 ? TotalComponentsPrice / Computer.Price : 0;
    }

    public class ComponentWithCharacteristics
    {
        public Component Component { get; set; }
        public Dictionary<string, string> Characteristics { get; set; } = new();
    }
}