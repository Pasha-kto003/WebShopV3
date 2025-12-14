using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebShopV3.Models;
using WebShopV3.Services;

namespace WebShopV3.Controllers
{
    public class CompareController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IComparisonService _comparisonService;

        public CompareController(ApplicationDbContext context, IComparisonService comparisonService)
        {
            _context = context;
            _comparisonService = comparisonService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var compareItems = GetCompareItemsFromSession();

                // Получаем только компьютеры для сравнения
                var computers = compareItems
                    .Where(x => x.Type == "Computer")
                    .Select(x => int.Parse(x.Id))
                    .ToList();

                if (computers.Any())
                {
                    // Загружаем компьютеры с компонентами
                    var computerEntities = await _context.Computers
                        .Where(c => computers.Contains(c.Id))
                        .Include(c => c.ComputerComponents)
                            .ThenInclude(cc => cc.Component)
                        .ToListAsync();

                    // Анализируем компьютеры
                    var analyzedComputers = await _comparisonService.AnalyzeComputers(computerEntities);

                    // Определяем лучший компьютер
                    var bestComputer = analyzedComputers.OrderByDescending(c => c.TotalScore).FirstOrDefault();
                    if (bestComputer != null)
                    {
                        bestComputer.IsBest = true;
                    }

                    // Получаем лучшие характеристики
                    var bestSpecs = _comparisonService.GetBestSpecifications(analyzedComputers);

                    ViewBag.AnalyzedComputers = analyzedComputers;
                    ViewBag.BestSpecs = bestSpecs;
                    ViewBag.HasAnalysis = true;
                }
                else
                {
                    ViewBag.HasAnalysis = false;
                }

                var viewModel = await CreateCompareViewModel(compareItems);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке страницы сравнения: {ex.Message}");
                ViewBag.HasAnalysis = false;
                return View(new CompareViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAnalysis(int computerId)
        {
            try
            {
                var computer = await _context.Computers
                    .Include(c => c.ComputerComponents)
                        .ThenInclude(cc => cc.Component)
                    .FirstOrDefaultAsync(c => c.Id == computerId);

                if (computer == null)
                    return NotFound();

                var analyzedComputer = (await _comparisonService.AnalyzeComputers(new List<Computer> { computer }))
                    .FirstOrDefault();

                return PartialView("_ComputerAnalysisPartial", analyzedComputer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении анализа: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productId, string productType)
        {
            try
            {
                var compareItems = GetCompareItemsFromSession();

                // Проверяем, не добавлен ли уже товар
                if (compareItems.Any(x => x.Id == productId.ToString() && x.Type == productType))
                {
                    TempData["ErrorMessage"] = "Товар уже добавлен в сравнение";
                    return RedirectToAction("Index", "Home");
                }

                // Проверяем лимит
                if (compareItems.Count >= 4)
                {
                    TempData["ErrorMessage"] = "Максимум 4 товара для сравнения";
                    return RedirectToAction("Index", "Home");
                }

                // Получаем данные товара
                var newItem = await GetCompareItemFromDatabase(productId, productType);
                if (newItem == null)
                {
                    TempData["ErrorMessage"] = "Товар не найден";
                    return RedirectToAction("Index", "Home");
                }

                compareItems.Add(newItem);
                SaveCompareItemsToSession(compareItems);

                TempData["SuccessMessage"] = "Товар добавлен в сравнение";
                return Redirect(Request.Headers["Referer"].ToString() ?? "/");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при добавлении в сравнение: {ex.Message}");
                TempData["ErrorMessage"] = "Ошибка при добавлении в сравнение";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int itemId)
        {
            try
            {
                var compareItems = GetCompareItemsFromSession();
                var itemToRemove = compareItems.FirstOrDefault(x => x.Id == itemId.ToString());

                if (itemToRemove != null)
                {
                    compareItems.Remove(itemToRemove);
                    SaveCompareItemsToSession(compareItems);
                    TempData["SuccessMessage"] = "Товар удален из сравнения";
                }
                else
                {
                    TempData["ErrorMessage"] = "Товар не найден";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при удалении из сравнения: {ex.Message}");
                TempData["ErrorMessage"] = "Ошибка при удалении";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            try
            {
                HttpContext.Session.Remove("CompareItems");
                TempData["SuccessMessage"] = "Список сравнения очищен";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при очистке сравнения: {ex.Message}");
                TempData["ErrorMessage"] = "Ошибка при очистке";
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<CompareViewModel> CreateCompareViewModel(List<CompareItem> items)
        {
            var viewModel = new CompareViewModel
            {
                Items = items,
                MaxItems = 4
            };

            // Получаем все уникальные характеристики
            viewModel.AllSpecs = GetAllSpecifications(items);

            return viewModel;
        }

        private async Task<CompareItem?> GetCompareItemFromDatabase(int productId, string productType)
        {
            CompareItem? newItem = null;

            if (productType == "Computer")
            {
                var computer = await _context.Computers
                    .Include(c => c.ComputerComponents)
                        .ThenInclude(cc => cc.Component)
                    .FirstOrDefaultAsync(c => c.Id == productId);

                if (computer != null)
                {
                    newItem = new CompareItem
                    {
                        Id = computer.Id.ToString(),
                        Name = computer.Name,
                        Type = "Computer",
                        ImageUrl = computer.ImageUrl ?? "default-computer.png",
                        Price = computer.Price,
                        Description = computer.Description,
                        Quantity = computer.Quantity,
                        Url = Url.Action("ComputerDetails", "Home", new { id = computer.Id }) ?? "#"
                    };

                    // Добавляем характеристики компьютера
                    newItem.Specifications["Категория"] = "Компьютер";
                    newItem.Specifications["Цена"] = computer.Price.ToString("C");
                    newItem.Specifications["Наличие"] = GetStockStatus(computer.Quantity);
                    newItem.Specifications["Количество"] = computer.Quantity.ToString();

                    // Получаем компоненты компьютера
                    if (computer.ComputerComponents != null)
                    {
                        var cpu = computer.ComputerComponents
                            .FirstOrDefault(cc => cc.Component?.Type == "CPU")?.Component;
                        if (cpu != null)
                            newItem.Specifications["Процессор"] = cpu.Name;

                        var gpu = computer.ComputerComponents
                            .FirstOrDefault(cc => cc.Component?.Type == "GPU")?.Component;
                        if (gpu != null)
                            newItem.Specifications["Видеокарта"] = gpu.Name;

                        var ram = computer.ComputerComponents
                            .FirstOrDefault(cc => cc.Component?.Type == "RAM")?.Component;
                        if (ram != null)
                            newItem.Specifications["Оперативная память"] = ram.Name;

                        var ssd = computer.ComputerComponents
                            .FirstOrDefault(cc => cc.Component?.Type == "SSD")?.Component;
                        if (ssd != null)
                            newItem.Specifications["Накопитель"] = ssd.Name;
                    }
                }
            }
            else if (productType == "Component")
            {
                var component = await _context.Components
                    .FirstOrDefaultAsync(c => c.Id == productId);

                if (component != null)
                {
                    newItem = new CompareItem
                    {
                        Id = component.Id.ToString(),
                        Name = component.Name,
                        Type = "Component",
                        ImageUrl = component.ImageUrl ?? "default-component.png",
                        Price = component.Price,
                        Description = component.Description,
                        Quantity = component.Quantity,
                        Url = Url.Action("ComponentDetails", "Home", new { id = component.Id }) ?? "#"
                    };

                    // Добавляем характеристики компонента
                    newItem.Specifications["Категория"] = component.Type;
                    newItem.Specifications["Цена"] = component.Price.ToString("C");
                    newItem.Specifications["Наличие"] = GetStockStatus(component.Quantity);
                    newItem.Specifications["Количество"] = component.Quantity.ToString();

                    if (!string.IsNullOrEmpty(component.Specifications))
                        newItem.Specifications["Характеристики"] = component.Specifications;

                    if (!string.IsNullOrEmpty(component.Socket))
                        newItem.Specifications["Socket"] = component.Socket;

                    if (!string.IsNullOrEmpty(component.MemoryType))
                        newItem.Specifications["Тип памяти"] = component.MemoryType;

                    if (!string.IsNullOrEmpty(component.FormFactor))
                        newItem.Specifications["Форм-фактор"] = component.FormFactor;
                }
            }

            return newItem;
        }

        private string GetStockStatus(int quantity)
        {
            return quantity > 10 ? "В наличии" :
                   quantity > 3 ? "Мало" : "Заканчивается";
        }

        private List<CompareItem> GetCompareItemsFromSession()
        {
            var compareJson = HttpContext.Session.GetString("CompareItems");
            if (!string.IsNullOrEmpty(compareJson))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<CompareItem>>(compareJson)
                           ?? new List<CompareItem>();
                }
                catch
                {
                    return new List<CompareItem>();
                }
            }
            return new List<CompareItem>();
        }

        private void SaveCompareItemsToSession(List<CompareItem> items)
        {
            var compareJson = JsonSerializer.Serialize(items);
            HttpContext.Session.SetString("CompareItems", compareJson);
        }

        private List<string> GetAllSpecifications(List<CompareItem> items)
        {
            var allSpecs = new HashSet<string>();

            foreach (var item in items)
            {
                foreach (var spec in item.Specifications.Keys)
                {
                    allSpecs.Add(spec);
                }
            }

            // Стандартные характеристики
            var defaultSpecs = new List<string>
            {
                "Категория",
                "Цена",
                "Наличие",
                "Количество"
            };

            foreach (var spec in defaultSpecs)
            {
                allSpecs.Add(spec);
            }

            return allSpecs.OrderBy(s => s).ToList();
        }
    }
}