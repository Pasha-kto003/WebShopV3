using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShopV3.Models;
using WebShopV3.Services;

namespace WebShopV3.Controllers
{
    [Authorize(Roles = "Админ,Менеджер")]
    public class ComputerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CompatibilityService _compatibilityService;

        public ComputerController(ApplicationDbContext context, CompatibilityService compatibilityService)
        {
            _context = context;
            _compatibilityService = compatibilityService;
        }

        // GET: Computer
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Computers.ToListAsync());
        }

        // GET: Computer/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var computer = await _context.Computers
            .Include(c => c.ComputerComponents)
                .ThenInclude(cc => cc.Component)
                    .ThenInclude(comp => comp.ComponentCharacteristics)
                        .ThenInclude(cc => cc.Characteristic)
            .FirstOrDefaultAsync(m => m.Id == id);

            if (computer == null)
            {
                return NotFound();
            }

            return View(computer);
        }

        [Authorize(Roles = "Админ")]
        public IActionResult Create()
        {
            return RedirectToAction("Index", "PcBuilder");
        }

        // GET: Computer/Edit/5 - Новый метод для редактирования через конфигуратор
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var computer = await _context.Computers
                .Include(c => c.ComputerComponents)
                    .ThenInclude(cc => cc.Component)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (computer == null) return NotFound();

            // Получаем ID выбранных компонентов
            var selectedComponentIds = computer.ComputerComponents
                .Select(cc => cc.ComponentId)
                .ToList();

            // Перенаправляем в конфигуратор с выбранными компонентами
            return RedirectToAction("EditConfiguration", "PcBuilder", new { computerId = id });
        }

        // POST: Computer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Create(Computer computer, int[] selectedComponents)
        {
            // Рассчитываем цену компьютера как сумму цен комплектующих + 10%
            if (selectedComponents != null && selectedComponents.Any())
            {
                decimal componentsTotalPrice = 0;
                foreach (var componentId in selectedComponents)
                {
                    var component = await _context.Components.FindAsync(componentId);
                    if (component != null)
                    {
                        componentsTotalPrice += component.Price;
                    }
                }

                // Добавляем 10% к сумме комплектующих
                computer.Price = componentsTotalPrice * 1.1m;
            }
            else
            {
                // Если комплектующие не выбраны, устанавливаем базовую цену
                computer.Price = 0;
            }

            _context.Add(computer);
            await _context.SaveChangesAsync();

            // Добавляем выбранные компоненты
            if (selectedComponents != null)
            {
                foreach (var componentId in selectedComponents)
                {
                    _context.ComputerComponents.Add(new ComputerComponent
                    {
                        ComputerId = computer.Id,
                        ComponentId = componentId
                    });
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Computer/Edit/5
        //[Authorize(Roles = "Админ")]
        //public async Task<IActionResult> Edit(int? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var computer = await _context.Computers
        //        .Include(c => c.ComputerComponents)
        //        .FirstOrDefaultAsync(m => m.Id == id);

        //    if (computer == null)
        //    {
        //        return NotFound();
        //    }

        //    ViewBag.Components = _context.Components.ToList();
        //    return View(computer);
        //}

        // POST: Computer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Edit(int id, Computer computer, int[] selectedComponents)
        {
            if (id != computer.Id)
            {
                return NotFound();
            }
            try
            {
                // Рассчитываем новую цену компьютера
                if (selectedComponents != null && selectedComponents.Any())
                {
                    decimal componentsTotalPrice = 0;
                    foreach (var componentId in selectedComponents)
                    {
                        var component = await _context.Components.FindAsync(componentId);
                        if (component != null)
                        {
                            componentsTotalPrice += component.Price;
                        }
                    }

                    // Добавляем 10% к сумме комплектующих
                    computer.Price = componentsTotalPrice * 1.1m;
                }
                else
                {
                    // Если комплектующие не выбраны, устанавливаем базовую цену
                    computer.Price = 0;
                }

                _context.Update(computer);

                // Обновляем компоненты
                var existingComponents = _context.ComputerComponents
                    .Where(cc => cc.ComputerId == computer.Id);
                _context.ComputerComponents.RemoveRange(existingComponents);

                if (selectedComponents != null)
                {
                    foreach (var componentId in selectedComponents)
                    {
                        _context.ComputerComponents.Add(new ComputerComponent
                        {
                            ComputerId = computer.Id,
                            ComponentId = componentId
                        });
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ComputerExists(computer.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            ViewBag.Components = _context.Components.ToList();
            return RedirectToAction(nameof(Index));    
        }

        // GET: Computer/Delete/5
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var computer = await _context.Computers
                .FirstOrDefaultAsync(m => m.Id == id);

            if (computer == null)
            {
                return NotFound();
            }

            return View(computer);
        }

        // POST: Computer/Delete/5
        [Authorize(Roles = "Админ")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var computer = await _context.Computers.FindAsync(id);
            _context.Computers.Remove(computer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ComputerExists(int id)
        {
            return _context.Computers.Any(e => e.Id == id);
        }
    }
}
