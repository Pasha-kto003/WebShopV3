using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShopV3.Models;
using WebShopV3.Services;

namespace WebShopV3.Controllers
{
    public class PcBuilderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CompatibilityService _compatibilityService;

        public PcBuilderController(ApplicationDbContext context, CompatibilityService compatibilityService)
        {
            _context = context;
            _compatibilityService = compatibilityService;
        }

        // GET: PcBuilder - Основная страница конфигуратора
        public async Task<IActionResult> Index()
        {
            var components = await _context.Components
                .Include(c => c.ComponentCharacteristics)
                    .ThenInclude(cc => cc.Characteristic)
                .ToListAsync();

            ViewBag.Components = components;
            return View();
        }

        // GET: PcBuilder/EditConfiguration - Редактирование существующего компьютера
        public async Task<IActionResult> EditConfiguration(int computerId)
        {
            var computer = await _context.Computers
                .Include(c => c.ComputerComponents)
                    .ThenInclude(cc => cc.Component)
                .FirstOrDefaultAsync(c => c.Id == computerId);

            if (computer == null)
            {
                return NotFound();
            }

            var components = await _context.Components
                .Include(c => c.ComponentCharacteristics)
                    .ThenInclude(cc => cc.Characteristic)
                .ToListAsync();

            ViewBag.Components = components;
            ViewBag.ComputerId = computerId;
            ViewBag.SelectedComponentIds = computer.ComputerComponents.Select(cc => cc.ComponentId).ToList();
            ViewBag.ComputerName = computer.Name;
            ViewBag.ComputerDescription = computer.Description;
            
            var filePath = Path.Combine("/Images/computers/", computer.ImageUrl);
            ViewBag.ComputerImageUrl = filePath;

            return View("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CheckCompatibility([FromBody] List<int> componentIds)
        {
            var selectedComponents = await _context.Components
                .Include(c => c.ComponentCharacteristics)
                    .ThenInclude(cc => cc.Characteristic)
                .Where(c => componentIds.Contains(c.Id))
                .ToListAsync();

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
                // Проверяем совместимость перед сохранением
                var selectedComponents = await _context.Components
                    .Include(c => c.ComponentCharacteristics)
                        .ThenInclude(cc => cc.Characteristic)
                    .Where(c => ComponentIds.Contains(c.Id))
                    .ToListAsync();

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

                // 1. ПРОВЕРЯЕМ НАЛИЧИЕ КОМПЛЕКТУЮЩИХ НА СКЛАДЕ
                var stockIssues = new List<string>();
                foreach (var component in selectedComponents)
                {
                    // Проверяем, что на складе есть хотя бы 1 экземпляр каждого компонента
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

                // Добавляем наценку 10%
                decimal finalPrice = componentsTotalPrice * 1.1m;

                Console.WriteLine($"Price calculation: Components total = {componentsTotalPrice:C}, Final price with 10% markup = {finalPrice:C}");

                string imageUrl = "1.jpg"; // изображение по умолчанию

                // Обработка загрузки изображения
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    // Создаем уникальное имя файла
                    var filePath = Path.Combine("wwwroot/Images/computers", ImageFile.FileName);

                    ViewBag.ComputerImageUrl = filePath;

                    // Создаем директорию если не существует
                    var directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Сохраняем файл
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

                // Используем Execution Strategy для транзакций с ретраями
                var executionStrategy = _context.Database.CreateExecutionStrategy();

                Computer computer = null;

                await executionStrategy.ExecuteAsync(async () =>
                {
                    // Начинаем транзакцию внутри execution strategy
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        if (ComputerId.HasValue)
                        {
                            // РЕДАКТИРОВАНИЕ существующего компьютера
                            computer = await _context.Computers
                                .Include(c => c.ComputerComponents)
                                    .ThenInclude(cc => cc.Component)
                                .FirstOrDefaultAsync(c => c.Id == ComputerId.Value);

                            if (computer == null)
                            {
                                throw new Exception("Компьютер не найден");
                            }

                            // ВОЗВРАЩАЕМ СТАРЫЕ КОМПЛЕКТУЮЩИЕ НА СКЛАД
                            foreach (var oldComponent in computer.ComputerComponents)
                            {
                                var component = oldComponent.Component;
                                if (component != null)
                                {
                                    component.Quantity += 1; // Возвращаем на склад
                                    _context.Components.Update(component);
                                    Console.WriteLine($"Returned to stock: {component.Name}, New quantity: {component.Quantity}");
                                }
                            }

                            // Обновляем данные компьютера
                            computer.Name = Name;
                            computer.Description = Description;
                            computer.Price = finalPrice;
                            computer.Quantity = Quantity;

                            // Обновляем изображение только если загружено новое
                            if (ImageFile != null)
                            {
                                computer.ImageUrl = imageUrl;
                            }

                            // Удаляем старые связи
                            _context.ComputerComponents.RemoveRange(computer.ComputerComponents);
                        }
                        else
                        {
                            // СОЗДАНИЕ нового компьютера
                            computer = new Computer
                            {
                                Name = Name,
                                Description = Description,
                                Price = finalPrice,
                                Quantity = Quantity,
                                ImageUrl = imageUrl
                            };

                            _context.Computers.Add(computer);
                        }

                        await _context.SaveChangesAsync();

                        // 2. СПИСЫВАЕМ НОВЫЕ КОМПЛЕКТУЮЩИЕ СО СКЛАДА
                        foreach (var component in selectedComponents)
                        {
                            // Уменьшаем количество на складе на 1
                            component.Quantity -= 1;
                            _context.Components.Update(component);

                            Console.WriteLine($"Deducted from stock: {component.Name}, New quantity: {component.Quantity}");

                            // Создаем связь компьютер-комплектующее
                            var computerComponent = new ComputerComponent
                            {
                                ComputerId = computer.Id,
                                ComponentId = component.Id
                            };
                            _context.ComputerComponents.Add(computerComponent);
                        }

                        await _context.SaveChangesAsync();

                        // Фиксируем транзакцию
                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        // При ошибке транзакция автоматически откатится при using
                        Console.WriteLine($"Transaction error: {ex.Message}");
                        throw; // Пробрасываем дальше
                    }
                });

                // Если мы здесь, значит транзакция успешно завершена
                return Json(new
                {
                    success = true,
                    computerId = computer?.Id ?? 0,
                    message = ComputerId.HasValue
                        ? "Конфигурация обновлена. Склад скорректирован."
                        : "Конфигурация сохранена. Комплектующие списаны со склада.",
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
                    message = $"Ошибка при сохранении конфигурации: {ex.Message}"
                });
            }
        }
    }

    public class ComputerConfiguration
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal TotalPrice { get; set; }
        public List<int> ComponentIds { get; set; } = new List<int>();
        public int? ComputerId { get; set; } // Для редактирования существующего компьютера
    }
}