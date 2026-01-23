using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebShopV3.Json.Models;
using WebShopV3.Json.Services;

namespace WebShopV3.Json.Controllers
{
    public class PcBuilderController : Controller
    {
        private readonly JsonDataService _jsonData;
        private readonly CompatibilityService _compatibilityService;

        public PcBuilderController(JsonDataService jsonData, CompatibilityService compatibilityService)
        {
            _jsonData = jsonData;
            _compatibilityService = compatibilityService;
        }

        // GET: PcBuilder - Основная страница конфигуратора
        public async Task<IActionResult> Index()
        {
            var components = await GetComponentsWithCharacteristics();
            ViewBag.Components = components;
            return View();
        }

        // GET: PcBuilder/MyConfigurations - Мои конфигурации
        [Authorize]
        public async Task<IActionResult> MyConfigurations()
        {
            try
            {
                var userId = User.Identity?.Name; // или User.FindFirstValue(ClaimTypes.NameIdentifier);

                var computers = await _jsonData.GetAllAsync<Computer>("Computers");

                // Фильтруем компьютеры по создателю
                var myComputers = computers
                    .Where(c => c.CreatedBy == userId)
                    .ToList();

                // Загружаем компоненты для каждого компьютера
                var allComputerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
                var allComponents = await _jsonData.GetAllAsync<Component>("Components");

                foreach (var computer in myComputers)
                {
                    var componentIds = allComputerComponents
                        .Where(cc => cc.ComputerId == computer.Id)
                        .Select(cc => cc.ComponentId)
                        .ToList();

                    computer.ComputerComponents = allComponents
                        .Where(c => componentIds.Contains(c.Id))
                        .Select(c => new ComputerComponent
                        {
                            ComputerId = computer.Id,
                            ComponentId = c.Id,
                            Component = c
                        })
                        .ToList();
                }

                return View(myComputers);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Не удалось загрузить ваши конфигурации";
                return View(new List<Computer>());
            }
        }

        // GET: PcBuilder/CloneConfiguration - Клонирование конфигурации
        [Authorize]
        public async Task<IActionResult> CloneConfiguration(int computerId)
        {
            try
            {
                var computer = await GetComputerWithComponents(computerId);
                if (computer == null)
                {
                    return NotFound();
                }

                var components = await GetComponentsWithCharacteristics();
                ViewBag.Components = components;

                // Не передаем ComputerId для создания новой конфигурации
                ViewBag.SelectedComponentIds = computer.ComputerComponents.Select(cc => cc.ComponentId).ToList();
                ViewBag.ComputerName = $"{computer.Name} (Копия)";
                ViewBag.ComputerDescription = computer.Description;
                ViewBag.IsClone = true;

                return View("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Не удалось клонировать конфигурацию";
                return RedirectToAction("MyConfigurations");
            }
        }

        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> EditConfiguration(int computerId)
        {
            var computer = await GetComputerWithComponents(computerId);
            if (computer == null)
            {
                return NotFound();
            }

            var userId = User.Identity?.Name;
            var isAdmin = User.IsInRole("Админ") || User.IsInRole("Менеджер");

            if (!isAdmin && computer.CreatedBy != userId)
            {
                TempData["ErrorMessage"] = "Вы можете редактировать только свои конфигурации";
                return RedirectToAction("MyConfigurations");
            }

            // Дополнительная проверка: только админ или менеджер может редактировать
            if (!User.IsInRole("Админ") && !User.IsInRole("Менеджер"))
            {
                TempData["ErrorMessage"] = "У вас нет прав для редактирования конфигураций";
                return RedirectToAction("Index");
            }

            var components = await GetComponentsWithCharacteristics();
            ViewBag.Components = components;
            ViewBag.ComputerId = computerId;
            ViewBag.SelectedComponentIds = computer.ComputerComponents.Select(cc => cc.ComponentId).ToList();
            ViewBag.ComputerName = computer.Name;
            ViewBag.ComputerDescription = computer.Description;

            var filePath = Path.Combine("/Images/computers/", computer.ImageUrl ?? "1.jpg");
            ViewBag.ComputerImageUrl = filePath;

            return View("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CheckCompatibility([FromBody] List<int> componentIds)
        {
            var selectedComponents = await GetComponentsByIds(componentIds);
            var result = _compatibilityService.CheckCompatibility(selectedComponents);
            return Json(result);
        }

        private string GenerateDetailedComputerDescription(List<Component> components)
        {
            var descriptionParts = new List<string>();

            foreach (var component in components.OrderBy(c => c.Type))
            {
                var componentInfo = GetComponentInfo(component);
                if (!string.IsNullOrEmpty(componentInfo))
                {
                    descriptionParts.Add(componentInfo);
                }
            }

            return string.Join(", ", descriptionParts);
        }

        private string GetComponentInfo(Component component)
        {
            return component.Type switch
            {
                "MB" => $"Материнская плата: {component.Name}",
                "CPU" => $"Процессор: {component.Name}",
                "RAM" => $"Оперативная память: {component.Name}",
                "GPU" => $"Видеокарта: {component.Name}",
                "PSU" => $"Блок питания: {component.Name}",
                "Case" => $"Корпус: {component.Name}",
                "SSD" => $"SSD накопитель: {component.Name}",
                "HDD" => $"Жесткий диск: {component.Name}",
                "Cooler" => $"Охлаждение: {component.Name}",
                _ => component.Name
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveConfiguration(
            string Name,
            string Description,
            decimal Price,
            int Quantity,
            List<int> ComponentIds,
            int? ComputerId,
            IFormFile ImageFile = null)
        {
            try
            {
                if (!User.Identity?.IsAuthenticated ?? true)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Для сохранения конфигурации необходимо авторизоваться"
                    });
                }


                // Проверяем совместимость перед сохранением
                var selectedComponents = await GetComponentsByIds(ComponentIds);
                var compatibilityResult = _compatibilityService.CheckCompatibility(selectedComponents);

                if (!compatibilityResult.IsCompatible)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Компоненты несовместимы",
                        errors = compatibilityResult.Errors
                    });
                }

                // Проверяем наличие комплектующих на складе
                var stockIssues = new List<string>();
                foreach (var component in selectedComponents)
                {
                    if (component.Quantity < 1)
                    {
                        stockIssues.Add(
                            $"Недостаточно '{component.Name}' в наличии. " +
                            $"Доступно: {component.Quantity}, Требуется: 1"
                        );
                    }
                }

                if (stockIssues.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Недостаточно комплектующих на складе",
                        errors = stockIssues
                    });
                }

                decimal componentsTotalPrice = selectedComponents.Sum(c => c.Price);
                decimal finalPrice = componentsTotalPrice * 1.1m;

                string imageUrl = "1.jpg"; // изображение по умолчанию

                // Обработка загрузки изображения
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var filePath = Path.Combine("wwwroot/Images/computers", ImageFile.FileName);
                    ViewBag.ComputerImageUrl = filePath;

                    var directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }

                    imageUrl = ImageFile.FileName;
                }

                if (string.IsNullOrWhiteSpace(Description))
                {
                    Description = GenerateDetailedComputerDescription(selectedComponents);
                }

                // Начинаем обработку с учетом JSON
                if (ComputerId.HasValue)
                {
                    // РЕДАКТИРОВАНИЕ существующего компьютера
                    return await UpdateExistingComputer(
                        ComputerId.Value,
                        Name,
                        Description,
                        finalPrice,
                        Quantity,
                        imageUrl,
                        selectedComponents);
                }
                else
                {
                    // СОЗДАНИЕ нового компьютера
                    return await CreateNewComputer(
                        Name,
                        Description,
                        finalPrice,
                        Quantity,
                        imageUrl,
                        selectedComponents);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Ошибка при сохранении конфигурации: {ex.Message}"
                });
            }
        }

        private async Task<JsonResult> UpdateExistingComputer(int computerId, string name, string description, decimal finalPrice, int quantity, string imageUrl, List<Component> selectedComponents)
        {
            try
            {
                // Получаем данные
                var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                var components = await _jsonData.GetAllAsync<Component>("Components");
                var allComputerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");

                // Находим компьютер
                var computer = computers.FirstOrDefault(c => c.Id == computerId);
                if (computer == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Компьютер не найден"
                    });
                }

                var oldComponentRelations = allComputerComponents
                    .Where(cc => cc.ComputerId == computerId)
                    .ToList();

                foreach (var relation in oldComponentRelations)
                {
                    var oldComponent = components.FirstOrDefault(c => c.Id == relation.ComponentId);
                    if (oldComponent != null)
                    {
                        oldComponent.Quantity += 1; 
                        await _jsonData.UpdateAsync("Components", oldComponent);
                    }
                }

                computer.Name = name;
                computer.Description = description;
                computer.Price = finalPrice;
                computer.Quantity = quantity;

                if (!string.IsNullOrEmpty(imageUrl) && imageUrl != "1.jpg")
                {
                    computer.ImageUrl = imageUrl;
                }

                await _jsonData.UpdateAsync("Computers", computer);

                var updatedComputerComponents = allComputerComponents
                    .Where(cc => cc.ComputerId != computerId)
                    .ToList();

                foreach (var component in selectedComponents)
                {
                    var dbComponent = components.FirstOrDefault(c => c.Id == component.Id);
                    if (dbComponent != null)
                    {
                        dbComponent.Quantity -= 1;
                        await _jsonData.UpdateAsync("Components", dbComponent);

                        updatedComputerComponents.Add(new ComputerComponent
                        {
                            ComputerId = computerId,
                            ComponentId = component.Id
                        });
                    }
                }

                await _jsonData.SaveAllAsync("ComputerComponents", updatedComputerComponents);

                return Json(new
                {
                    success = true,
                    computerId = computer.Id,
                    message = "Конфигурация обновлена. Склад скорректирован.",
                    calculatedPrice = finalPrice,
                    componentsUsed = selectedComponents.Count,
                    stockUpdated = true
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Ошибка при обновлении конфигурации: {ex.Message}"
                });
            }
        }

        private async Task<JsonResult> CreateNewComputer(
            string name,
            string description,
            decimal finalPrice,
            int quantity,
            string imageUrl,
            List<Component> selectedComponents)
        {
            try
            {
                // Создаем новый компьютер
                var computer = new Computer
                {
                    Name = name,
                    Description = description,
                    Price = finalPrice,
                    Quantity = quantity,
                    ImageUrl = imageUrl,
                    CreatedBy = User.Identity?.Name
                };

                await _jsonData.CreateAsync("Computers", computer);

                // СПИСЫВАЕМ КОМПЛЕКТУЮЩИЕ СО СКЛАДА
                var components = await _jsonData.GetAllAsync<Component>("Components");
                var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");

                foreach (var component in selectedComponents)
                {
                    var dbComponent = components.FirstOrDefault(c => c.Id == component.Id);
                    if (dbComponent != null)
                    {
                        dbComponent.Quantity -= 1; // Списываем со склада
                        await _jsonData.UpdateAsync("Components", dbComponent);

                        // Создаем связь компьютер-комплектующее
                        computerComponents.Add(new ComputerComponent
                        {
                            ComputerId = computer.Id,
                            ComponentId = component.Id
                        });
                    }
                }

                await _jsonData.SaveAllAsync("ComputerComponents", computerComponents);

                return Json(new
                {
                    success = true,
                    computerId = computer.Id,
                    message = "Конфигурация сохранена. Комплектующие списаны со склада.",
                    calculatedPrice = finalPrice,
                    componentsUsed = selectedComponents.Count,
                    stockUpdated = true
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Ошибка при создании конфигурации: {ex.Message}"
                });
            }
        }



        private async Task<List<Component>> GetComponentsWithCharacteristics()
        {
            var components = await _jsonData.GetAllAsync<Component>("Components");
            var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");
            var characteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");

            foreach (var component in components)
            {
                var charIds = componentCharacteristics
                    .Where(cc => cc.ComponentId == component.Id)
                    .Select(cc => cc.CharacteristicId)
                    .ToList();

                component.ComponentCharacteristics = characteristics
                    .Where(c => charIds.Contains(c.Id))
                    .Select(c => new ComponentCharacteristic
                    {
                        ComponentId = component.Id,
                        CharacteristicId = c.Id,
                        Characteristic = c,
                        Value = componentCharacteristics
                            .FirstOrDefault(cc => cc.ComponentId == component.Id && cc.CharacteristicId == c.Id)?
                            .Value ?? ""
                    })
                    .ToList();
            }

            return components;
        }

        private async Task<Computer?> GetComputerWithComponents(int computerId)
        {
            var computer = await _jsonData.GetByIdAsync<Computer>("Computers", computerId);
            if (computer == null) return null;

            var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
            var components = await _jsonData.GetAllAsync<Component>("Components");

            var componentIds = computerComponents
                .Where(cc => cc.ComputerId == computerId)
                .Select(cc => cc.ComponentId)
                .ToList();

            computer.ComputerComponents = components
                .Where(c => componentIds.Contains(c.Id))
                .Select(c => new ComputerComponent
                {
                    ComputerId = computerId,
                    ComponentId = c.Id,
                    Component = c
                })
                .ToList();

            return computer;
        }

        private async Task<List<Component>> GetComponentsByIds(List<int> componentIds)
        {
            var components = await GetComponentsWithCharacteristics();
            return components
                .Where(c => componentIds.Contains(c.Id))
                .ToList();
        }
    }

    public class ComputerConfiguration
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal TotalPrice { get; set; }
        public List<int> ComponentIds { get; set; } = new List<int>();
        public int? ComputerId { get; set; }
    }
}