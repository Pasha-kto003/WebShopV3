using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebShopV3.Models;

namespace WebShopV3.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var computers = await _context.Computers
                .Where(c => c.Quantity > 0)
                .Take(8)
                .ToListAsync();

            return View(computers);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Catalog(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice)
        {
            ViewBag.SearchQuery = search;
            ViewBag.SortBy = sortBy;
            ViewBag.ComponentType = componentType;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            // Получаем все доступные типы комплектующих для фильтра
            ViewBag.ComponentTypes = await _context.Components
                .Select(c => c.Type)
                .Distinct()
                .ToListAsync();

            var computers = await GetFilteredComputers(search, sortBy, componentType, minPrice, maxPrice);

            return View(computers);
        }

        public async Task<IActionResult> SearchComputers(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice)
        {
            var computers = await GetFilteredComputers(search, sortBy, componentType, minPrice, maxPrice);
            return PartialView("_ComputerListPartial", computers);
        }

        private async Task<List<Computer>> GetFilteredComputers(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice)
        {
            var query = _context.Computers
                .Include(c => c.ComputerComponents)
                .ThenInclude(cc => cc.Component)
                .Where(c => c.Quantity > 0)
                .AsQueryable();

            // Поиск по названию и описанию
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    c.Description.ToLower().Contains(search) ||
                    c.ComputerComponents.Any(cc => cc.Component.Name.ToLower().Contains(search))
                );
            }

            // Фильтр по типу комплектующих
            if (!string.IsNullOrEmpty(componentType) && componentType != "all")
            {
                query = query.Where(c =>
                    c.ComputerComponents.Any(cc => cc.Component.Type == componentType)
                );
            }

            // Фильтр по цене
            if (minPrice.HasValue)
            {
                query = query.Where(c => c.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(c => c.Price <= maxPrice.Value);
            }

            // Сортировка
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(c => c.Price),
                "price_desc" => query.OrderByDescending(c => c.Price),
                "name_asc" => query.OrderBy(c => c.Name),
                "name_desc" => query.OrderByDescending(c => c.Name),
                "newest" => query.OrderByDescending(c => c.Id),
                _ => query.OrderBy(c => c.Id) // по умолчанию
            };

            return await query.ToListAsync();
        }

        [AllowAnonymous]
        public async Task<IActionResult> ComputerDetails(int? id)
        {
            if (id == null) return NotFound();

            var computer = await _context.Computers
                .Include(c => c.ComputerComponents)
                    .ThenInclude(cc => cc.Component)
                        .ThenInclude(comp => comp.ComponentCharacteristics)
                            .ThenInclude(cc => cc.Characteristic)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (computer == null) return NotFound();

            return View(computer);
        }

        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Contact()
        {
            return View();
        }
    }
}
