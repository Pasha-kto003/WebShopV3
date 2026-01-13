using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;

namespace WebShopV3.Controllers
{
    [Authorize(Roles = "Админ,Менеджер")]
    public class ComponentController : Controller
    {
        private readonly JsonDataService _jsonData;

        public ComponentController(JsonDataService jsonData)
        {
            _jsonData = jsonData;
        }

        // GET: Component
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Index()
        {
            var components = await _jsonData.GetAllAsync<Component>("Components");
            return View(components);
        }

        // GET: Component/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var component = await GetComponentWithCharacteristics(id.Value);
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
            // Устанавливаем значения по умолчанию
            component.FormFactor = "";
            component.MaxMemory = 0;
            component.MemorySlots = 0;
            component.MemoryType = "";
            component.PowerConnector = "";
            component.Socket = "";

            await _jsonData.CreateAsync("Components", component);
            return RedirectToAction(nameof(Index));
        }

        // GET: Component/Edit/5
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var component = await GetComponentWithCharacteristics(id.Value);
            if (component == null) return NotFound();

            // Получаем доступные характеристики
            ViewBag.AvailableCharacteristics = await GetAvailableCharacteristics(component.Id);
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
                    // Устанавливаем значения по умолчанию для null полей
                    component.FormFactor ??= "";
                    component.MaxMemory ??= 0;
                    component.MemorySlots ??= 0;
                    component.MemoryType ??= "";
                    component.PowerConnector ??= "";
                    component.Socket ??= "";

                    var success = await _jsonData.UpdateAsync("Components", component);
                    if (!success) return NotFound();

                    TempData["Success"] = "Комплектующее успешно обновлено";
                    return RedirectToAction(nameof(Details), new { id = component.Id });
                }
                catch (Exception)
                {
                    if (!await ComponentExists(component.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            // Если валидация не прошла, перезагружаем доступные характеристики
            ViewBag.AvailableCharacteristics = await GetAvailableCharacteristics(id);
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
                var component = await _jsonData.GetByIdAsync<Component>("Components", componentId);
                var characteristic = await _jsonData.GetByIdAsync<Characteristic>("Characteristics", characteristicId);

                if (component == null || characteristic == null)
                {
                    TempData["Error"] = "Комплектующее или характеристика не найдены";
                    return RedirectToAction(nameof(Edit), new { id = componentId });
                }

                // Получаем все связи компонентов с характеристиками
                var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");

                // Проверяем, не добавлена ли уже эта характеристика
                var existing = componentCharacteristics
                    .FirstOrDefault(cc => cc.ComponentId == componentId && cc.CharacteristicId == characteristicId);

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

                await _jsonData.CreateAsync("ComponentCharacteristics", componentCharacteristic);
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
                // Получаем все связи
                var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");

                // Находим нужную связь
                var componentCharacteristic = componentCharacteristics
                    .FirstOrDefault(cc => cc.ComponentId == componentId && cc.CharacteristicId == characteristicId);

                if (componentCharacteristic == null)
                {
                    TempData["Error"] = "Характеристика не найдена";
                    return RedirectToAction(nameof(Edit), new { id = componentId });
                }

                // Удаляем связь (нужен метод удаления по составному ключу)
                var success = await DeleteComponentCharacteristic(componentId, characteristicId);
                if (!success)
                {
                    TempData["Error"] = "Ошибка при удалении характеристики";
                    return RedirectToAction(nameof(Edit), new { id = componentId });
                }

                TempData["Success"] = "Характеристика успешно удалена";
                return RedirectToAction(nameof(Edit), new { id = componentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка при удалении характеристики: {ex.Message}";
                return RedirectToAction(nameof(Edit), new { id = componentId });
            }
        }

        private async Task<bool> ComponentExists(int id)
        {
            var component = await _jsonData.GetByIdAsync<Component>("Components", id);
            return component != null;
        }

        public async Task<JsonResult> GetAvailableCharacteristics()
        {
            var characteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");

            var result = characteristics
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    unit = c.Unit,
                    description = c.Description
                })
                .ToList();

            return Json(result);
        }

        // GET: Component/Delete/5
        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var component = await _jsonData.GetByIdAsync<Component>("Components", id.Value);
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
            // Сначала удаляем все связи с характеристиками
            await DeleteAllComponentCharacteristics(id);

            // Затем удаляем сам компонент
            var success = await _jsonData.DeleteAsync<Component>("Components", id);
            if (!success) return NotFound();

            return RedirectToAction(nameof(Index));
        }

        // Вспомогательные методы

        private async Task<Component?> GetComponentWithCharacteristics(int componentId)
        {
            var component = await _jsonData.GetByIdAsync<Component>("Components", componentId);
            if (component == null) return null;

            // Загружаем характеристики компонента
            var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");
            var characteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");

            // Находим все связи для этого компонента
            var characteristicIds = componentCharacteristics
                .Where(cc => cc.ComponentId == componentId)
                .Select(cc => cc.CharacteristicId)
                .ToList();

            // Получаем характеристики
            var componentCharacteristicsList = characteristics
                .Where(c => characteristicIds.Contains(c.Id))
                .ToList();

            // Создаем коллекцию ComponentCharacteristic с заполненными характеристиками
            component.ComponentCharacteristics = componentCharacteristicsList
                .Select(characteristic => new ComponentCharacteristic
                {
                    ComponentId = componentId,
                    CharacteristicId = characteristic.Id,
                    Characteristic = characteristic,
                    Value = componentCharacteristics
                        .FirstOrDefault(cc => cc.ComponentId == componentId && cc.CharacteristicId == characteristic.Id)?
                        .Value ?? ""
                })
                .ToList();

            return component;
        }

        private async Task<List<Characteristic>> GetAvailableCharacteristics(int componentId)
        {
            var allCharacteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");
            var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");

            // Получаем ID характеристик, уже добавленных к компоненту
            var existingCharacteristicIds = componentCharacteristics
                .Where(cc => cc.ComponentId == componentId)
                .Select(cc => cc.CharacteristicId)
                .ToList();

            // Возвращаем характеристики, которые еще не добавлены
            return allCharacteristics
                .Where(c => !existingCharacteristicIds.Contains(c.Id))
                .ToList();
        }

        private async Task<bool> DeleteComponentCharacteristic(int componentId, int characteristicId)
        {
            var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");

            var itemToRemove = componentCharacteristics
                .FirstOrDefault(cc => cc.ComponentId == componentId && cc.CharacteristicId == characteristicId);

            if (itemToRemove == null) return false;

            componentCharacteristics.Remove(itemToRemove);
            await _jsonData.SaveAllAsync("ComponentCharacteristics", componentCharacteristics);
            return true;
        }

        private async Task DeleteAllComponentCharacteristics(int componentId)
        {
            var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");

            var itemsToRemove = componentCharacteristics
                .Where(cc => cc.ComponentId == componentId)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                componentCharacteristics.Remove(item);
            }

            await _jsonData.SaveAllAsync("ComponentCharacteristics", componentCharacteristics);
        }
    }
}