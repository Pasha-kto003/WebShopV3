using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;

namespace WebShopV3.Json.Controllers
{
    [Authorize]
    public class CharacteristicController : Controller
    {
        private readonly JsonDataService _jsonData;

        public CharacteristicController(JsonDataService jsonData)
        {
            _jsonData = jsonData;
        }

        // GET: Characteristic
        public async Task<IActionResult> Index()
        {
            var characteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");
            return View(characteristics);
        }

        // GET: Characteristic/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var characteristic = await _jsonData.GetByIdAsync<Characteristic>("Characteristics", id.Value);

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
                await _jsonData.CreateAsync("Characteristics", characteristic);
                return RedirectToAction(nameof(Index));
            }
            return View(characteristic);
        }

        // GET: Characteristic/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var characteristic = await _jsonData.GetByIdAsync<Characteristic>("Characteristics", id.Value);
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
                    var success = await _jsonData.UpdateAsync("Characteristics", characteristic);
                    if (!success) return NotFound();
                }
                catch (Exception)
                {
                    if (!await CharacteristicExists(characteristic.Id))
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

            var characteristic = await _jsonData.GetByIdAsync<Characteristic>("Characteristics", id.Value);

            if (characteristic == null) return NotFound();

            return View(characteristic);
        }

        // POST: Characteristic/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var success = await _jsonData.DeleteAsync<Characteristic>("Characteristics", id);
            if (!success) return NotFound();

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CharacteristicExists(int id)
        {
            var characteristic = await _jsonData.GetByIdAsync<Characteristic>("Characteristics", id);
            return characteristic != null;
        }
    }
}