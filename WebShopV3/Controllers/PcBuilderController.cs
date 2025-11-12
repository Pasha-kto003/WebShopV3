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

        public async Task<IActionResult> Index()
        {
            var components = await _context.Components
                .Include(c => c.ComponentCharacteristics)
                    .ThenInclude(cc => cc.Characteristic)
                .ToListAsync();

            ViewBag.Components = components;
            return View();
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
        public async Task<IActionResult> SaveConfiguration([FromBody] ComputerConfiguration config)
        {
            var computer = new Computer
            {
                Name = config.Name,
                Description = config.Description,
                Price = config.TotalPrice,
                Quantity = 1,
                ImageUrl = "default-pc.jpg"
            };

            _context.Computers.Add(computer);
            await _context.SaveChangesAsync();

            // Добавляем компоненты
            foreach (var componentId in config.ComponentIds)
            {
                var computerComponent = new ComputerComponent
                {
                    ComputerId = computer.Id,
                    ComponentId = componentId
                };
                _context.ComputerComponents.Add(computerComponent);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, computerId = computer.Id });
            
        }
    }

    public class ComputerConfiguration
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal TotalPrice { get; set; }
        public List<int> ComponentIds { get; set; } = new List<int>();
    }
}