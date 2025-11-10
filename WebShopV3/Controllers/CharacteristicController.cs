using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShopV3.Models;

namespace WebShopV3.Controllers
{
    [Authorize]
    public class CharacteristicController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CharacteristicController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Characteristic
        public async Task<IActionResult> Index()
        {
            return View(await _context.Characteristics.ToListAsync());
        }

        // GET: Characteristic/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var characteristic = await _context.Characteristics
                .FirstOrDefaultAsync(m => m.Id == id);

            if (characteristic == null) return NotFound();

            return View(characteristic);
        }

        // GET: Characteristic/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Characteristic/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Characteristic characteristic)
        {
            if (ModelState.IsValid)
            {
                _context.Add(characteristic);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(characteristic);
        }

        // GET: Characteristic/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var characteristic = await _context.Characteristics.FindAsync(id);
            if (characteristic == null) return NotFound();

            return View(characteristic);
        }

        // POST: Characteristic/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Characteristic characteristic)
        {
            if (id != characteristic.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(characteristic);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CharacteristicExists(characteristic.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(characteristic);
        }

        // GET: Characteristic/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var characteristic = await _context.Characteristics
                .FirstOrDefaultAsync(m => m.Id == id);

            if (characteristic == null) return NotFound();

            return View(characteristic);
        }

        // POST: Characteristic/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var characteristic = await _context.Characteristics.FindAsync(id);
            _context.Characteristics.Remove(characteristic);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CharacteristicExists(int id)
        {
            return _context.Characteristics.Any(e => e.Id == id);
        }
    }
}
