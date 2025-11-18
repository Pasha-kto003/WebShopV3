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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveConfiguration(string Name, string Description, decimal Price, int Quantity, List<int> ComponentIds, int? ComputerId, IFormFile ImageFile = null)
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

                decimal componentsTotalPrice = selectedComponents.Sum(c => c.Price);

                // Добавляем наценку 10%
                decimal finalPrice = componentsTotalPrice * 1.1m;

                Console.WriteLine($"Price calculation: Components total = {componentsTotalPrice:C}, Final price with 10% markup = {finalPrice:C}");

                Computer computer;
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

                if (ComputerId.HasValue)
                {
                    // Редактирование существующего компьютера
                    computer = await _context.Computers
                        .Include(c => c.ComputerComponents)
                        .FirstOrDefaultAsync(c => c.Id == ComputerId.Value);

                    if (computer == null)
                    {
                        return Json(new { success = false, message = "Компьютер не найден" });
                    }

                    // Обновляем данные компьютера
                    computer.Name = Name;
                    computer.Description = Description;
                    computer.Price = finalPrice;

                    // Обновляем изображение только если загружено новое
                    if (ImageFile != null)
                    {
                        computer.ImageUrl = imageUrl;
                    }

                    // Удаляем старые компоненты
                    _context.ComputerComponents.RemoveRange(computer.ComputerComponents);
                }
                else
                {
                    // Создание нового компьютера
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

                // Добавляем новые компоненты
                foreach (var componentId in ComponentIds)
                {
                    var computerComponent = new ComputerComponent
                    {
                        ComputerId = computer.Id,
                        ComponentId = componentId
                    };
                    _context.ComputerComponents.Add(computerComponent);
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    computerId = computer.Id,
                    message = ComputerId.HasValue ? "Конфигурация обновлена" : "Конфигурация сохранена",
                    calculatesPrice = finalPrice
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
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