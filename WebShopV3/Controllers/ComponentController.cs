using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShopV3.Models;

namespace WebShopV3.Controllers
{
    [Authorize(Roles = "Админ,Менеджер")]
    public class ComponentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ComponentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Component
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Components.ToListAsync());
        }

        // GET: Component/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var component = await _context.Components
             .Include(c => c.ComponentCharacteristics)
                 .ThenInclude(cc => cc.Characteristic)
             .FirstOrDefaultAsync(m => m.Id == id);

            if (component == null)
            {
                return NotFound();
            }

            return View(component);
        }

        // GET: Component/Create
        [Authorize(Roles = "Админ")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Component/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Create(Component component)
        {

            _context.Add(component);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Component/Edit/5
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var component = await _context.Components
                .Include(c => c.ComponentCharacteristics)
                    .ThenInclude(cc => cc.Characteristic)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (component == null) return NotFound();

            // ЗАПРОС ИСПРАВЛЕН: Сначала получаем ID уже добавленных характеристик
            var existingCharacteristicIds = component.ComponentCharacteristics
                .Select(cc => cc.CharacteristicId)
                .ToList();

            // Затем получаем доступные характеристики
            ViewBag.AvailableCharacteristics = await _context.Characteristics
                .Where(c => !existingCharacteristicIds.Contains(c.Id))
                .ToListAsync();

            return View(component);
        }

        // POST: Component/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Edit(int id, Component component)
        {
            if (id != component.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(component);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Комплектующее успешно обновлено";
                    return RedirectToAction(nameof(Details), new { id = component.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ComponentExists(component.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            // Если валидация не прошла, перезагружаем доступные характеристики
            var existingCharacteristicIds = await _context.ComponentCharacteristics
                .Where(cc => cc.ComponentId == id)
                .Select(cc => cc.CharacteristicId)
                .ToListAsync();

            ViewBag.AvailableCharacteristics = await _context.Characteristics
                .Where(c => !existingCharacteristicIds.Contains(c.Id))
                .ToListAsync();

            return View(component);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> AddCharacteristic(int componentId, int characteristicId, string value)
        {
            try
            {
                // Проверяем существование компонента и характеристики
                var component = await _context.Components.FindAsync(componentId);
                var characteristic = await _context.Characteristics.FindAsync(characteristicId);

                if (component == null || characteristic == null)
                {
                    TempData["Error"] = "Комплектующее или характеристика не найдены";
                    return RedirectToAction(nameof(Edit), new { id = componentId });
                }

                // Проверяем, не добавлена ли уже эта характеристика
                var existing = await _context.ComponentCharacteristics
                    .FirstOrDefaultAsync(cc => cc.ComponentId == componentId && cc.CharacteristicId == characteristicId);

                if (existing != null)
                {
                    TempData["Error"] = "Эта характеристика уже добавлена к компоненту";
                    return RedirectToAction(nameof(Edit), new { id = componentId });
                }

                // Добавляем новую характеристику
                var componentCharacteristic = new ComponentCharacteristic
                {
                    ComponentId = componentId,
                    CharacteristicId = characteristicId,
                    Value = value?.Trim() ?? ""
                };

                _context.ComponentCharacteristics.Add(componentCharacteristic);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Характеристика успешно добавлена";
                return RedirectToAction(nameof(Edit), new { id = componentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка при добавлении характеристики: {ex.Message}";
                return RedirectToAction(nameof(Edit), new { id = componentId });
            }
        }

        public async Task<IActionResult> DeleteCharacteristic(int componentId, int characteristicId)
        {
            try
            {
                var componentCharacteristic = await _context.ComponentCharacteristics
                    .FirstOrDefaultAsync(cc => cc.ComponentId == componentId && cc.CharacteristicId == characteristicId);

                if (componentCharacteristic == null)
                {
                    TempData["Error"] = "Характеристика не найдена";
                    return RedirectToAction(nameof(Edit), new { id = componentId });
                }

                _context.ComponentCharacteristics.Remove(componentCharacteristic);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Характеристика успешно удалена";
                return RedirectToAction(nameof(Edit), new { id = componentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка при удалении характеристики: {ex.Message}";
                return RedirectToAction(nameof(Edit), new { id = componentId });
            }
        }

        private bool ComponentExists(int id)
        {
            return _context.Components.Any(e => e.Id == id);
        }

        public async Task<JsonResult> GetAvailableCharacteristics()
        {
            var characteristics = await _context.Characteristics
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    unit = c.Unit,
                    description = c.Description
                })
                .ToListAsync();

            return Json(characteristics);
        }

        // GET: Component/Delete/5
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var component = await _context.Components
                .FirstOrDefaultAsync(m => m.Id == id);

            if (component == null)
            {
                return NotFound();
            }

            return View(component);
        }

        // POST: Component/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var component = await _context.Components.FindAsync(id);
            _context.Components.Remove(component);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
