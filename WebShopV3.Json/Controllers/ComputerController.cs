using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;

namespace WebShopV3.Controllers
{
    [Authorize(Roles = "Админ,Менеджер")]
    public class ComputerController : Controller
    {
        private readonly JsonDataService _jsonData;
        private readonly CompatibilityService _compatibilityService;

        public ComputerController(JsonDataService jsonData, CompatibilityService compatibilityService)
        {
            _jsonData = jsonData;
            _compatibilityService = compatibilityService;
        }

        // GET: Computer
        [Authorize(Roles = "Админ,Менеджер")]
        public async Task<IActionResult> Index()
        {
            var computers = await _jsonData.GetAllAsync<Computer>("Computers");
            return View(computers);
        }

        // GET: Computer/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var computer = await GetComputerWithDetails(id.Value);
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

            var computer = await GetComputerWithComponents(id.Value);
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
                var components = await _jsonData.GetAllAsync<Component>("Components");

                foreach (var componentId in selectedComponents)
                {
                    var component = components.FirstOrDefault(c => c.Id == componentId);
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

            await _jsonData.CreateAsync("Computers", computer);

            // Добавляем выбранные компоненты
            if (selectedComponents != null)
            {
                var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");

                foreach (var componentId in selectedComponents)
                {
                    computerComponents.Add(new ComputerComponent
                    {
                        ComputerId = computer.Id,
                        ComponentId = componentId
                    });
                }

                await _jsonData.SaveAllAsync("ComputerComponents", computerComponents);
            }

            return RedirectToAction(nameof(Index));
        }

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
                    var components = await _jsonData.GetAllAsync<Component>("Components");

                    foreach (var componentId in selectedComponents)
                    {
                        var component = components.FirstOrDefault(c => c.Id == componentId);
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

                await _jsonData.UpdateAsync("Computers", computer);

                // Обновляем компоненты
                var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");

                // Удаляем старые связи
                var componentsToRemove = computerComponents
                    .Where(cc => cc.ComputerId == computer.Id)
                    .ToList();

                foreach (var component in componentsToRemove)
                {
                    computerComponents.Remove(component);
                }

                // Добавляем новые связи
                if (selectedComponents != null)
                {
                    foreach (var componentId in selectedComponents)
                    {
                        computerComponents.Add(new ComputerComponent
                        {
                            ComputerId = computer.Id,
                            ComponentId = componentId
                        });
                    }
                }

                await _jsonData.SaveAllAsync("ComputerComponents", computerComponents);
            }
            catch (Exception)
            {
                if (!await ComputerExists(computer.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

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

            var computer = await _jsonData.GetByIdAsync<Computer>("Computers", id.Value);
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
            // Удаляем связи компьютера с компонентами
            await DeleteComputerComponents(id);

            // Удаляем сам компьютер
            var success = await _jsonData.DeleteAsync<Computer>("Computers", id);
            if (!success) return NotFound();

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> ComputerExists(int id)
        {
            var computer = await _jsonData.GetByIdAsync<Computer>("Computers", id);
            return computer != null;
        }

        // Вспомогательные методы

        private async Task<Computer?> GetComputerWithDetails(int computerId)
        {
            var computer = await _jsonData.GetByIdAsync<Computer>("Computers", computerId);
            if (computer == null) return null;

            // Загружаем компоненты компьютера с их характеристиками
            var computerComponents = await GetComputerComponentsWithDetails(computerId);
            computer.ComputerComponents = computerComponents;

            return computer;
        }

        private async Task<List<ComputerComponent>> GetComputerComponentsWithDetails(int computerId)
        {
            var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
            var components = await _jsonData.GetAllAsync<Component>("Components");
            var componentCharacteristics = await _jsonData.GetAllAsync<ComponentCharacteristic>("ComponentCharacteristics");
            var characteristics = await _jsonData.GetAllAsync<Characteristic>("Characteristics");

            // Получаем ID компонентов компьютера
            var componentIds = computerComponents
                .Where(cc => cc.ComputerId == computerId)
                .Select(cc => cc.ComponentId)
                .ToList();

            // Получаем компоненты
            var computerComponentsList = new List<ComputerComponent>();

            foreach (var componentId in componentIds)
            {
                var component = components.FirstOrDefault(c => c.Id == componentId);
                if (component == null) continue;

                // Получаем характеристики компонента
                var characteristicIds = componentCharacteristics
                    .Where(cc => cc.ComponentId == componentId)
                    .Select(cc => cc.CharacteristicId)
                    .ToList();

                var componentChars = characteristics
                    .Where(c => characteristicIds.Contains(c.Id))
                    .Select(c => new ComponentCharacteristic
                    {
                        ComponentId = componentId,
                        CharacteristicId = c.Id,
                        Characteristic = c,
                        Value = componentCharacteristics
                            .FirstOrDefault(cc => cc.ComponentId == componentId && cc.CharacteristicId == c.Id)?
                            .Value ?? ""
                    })
                    .ToList();

                component.ComponentCharacteristics = componentChars;

                computerComponentsList.Add(new ComputerComponent
                {
                    ComputerId = computerId,
                    ComponentId = componentId,
                    Component = component
                });
            }

            return computerComponentsList;
        }

        private async Task<Computer?> GetComputerWithComponents(int computerId)
        {
            var computer = await _jsonData.GetByIdAsync<Computer>("Computers", computerId);
            if (computer == null) return null;

            // Загружаем компоненты компьютера
            var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");
            var components = await _jsonData.GetAllAsync<Component>("Components");

            var componentIds = computerComponents
                .Where(cc => cc.ComputerId == computerId)
                .Select(cc => cc.ComponentId)
                .ToList();

            var computerComponentsList = components
                .Where(c => componentIds.Contains(c.Id))
                .Select(c => new ComputerComponent
                {
                    ComputerId = computerId,
                    ComponentId = c.Id,
                    Component = c
                })
                .ToList();

            computer.ComputerComponents = computerComponentsList;
            return computer;
        }

        private async Task DeleteComputerComponents(int computerId)
        {
            var computerComponents = await _jsonData.GetAllAsync<ComputerComponent>("ComputerComponents");

            var componentsToRemove = computerComponents
                .Where(cc => cc.ComputerId == computerId)
                .ToList();

            foreach (var component in componentsToRemove)
            {
                computerComponents.Remove(component);
            }

            await _jsonData.SaveAllAsync("ComputerComponents", computerComponents);
        }
    }
}