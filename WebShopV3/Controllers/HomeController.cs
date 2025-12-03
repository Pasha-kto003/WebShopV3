using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
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

            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;

            return View(computers);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Catalog(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice, string productType = "all")
        {
            ViewBag.SearchQuery = search;
            ViewBag.SortBy = sortBy;
            ViewBag.ComponentType = componentType;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.ProductType = productType;

            ViewBag.ComponentTypes = await _context.Components
                .Select(c => c.Type)
                .Distinct()
                .ToListAsync();

            var computers = await GetFilteredComputers(search, sortBy, componentType, minPrice, maxPrice);
            var components = await GetFilteredComponents(search, sortBy, componentType, minPrice, maxPrice);

            var viewModel = new CatalogViewModel
            {
                Computers = computers,
                Components = components,
                ProductType = productType
            };


            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;

            return View(viewModel);
        }


        [HttpGet]
        public async Task<IActionResult> SearchProducts(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice, string productType = "all")
        {
            var computers = await GetFilteredComputers(search, sortBy, componentType, minPrice, maxPrice);
            var components = await GetFilteredComponents(search, sortBy, componentType, minPrice, maxPrice);

            // ФИЛЬТРАЦИЯ ПО ТИПУ ТОВАРА
            if (productType == "computers")
            {
                components = new List<Component>(); // Очищаем комплектующие
            }
            else if (productType == "components")
            {
                computers = new List<Computer>(); // Очищаем компьютеры
            }
            
            // Если "all" - оставляем оба списка

            var viewModel = new CatalogViewModel
            {
                Computers = computers,
                Components = components,
                ProductType = productType
            };

            return PartialView("_ComputerListPartial", viewModel);
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

        private async Task<List<Component>> GetFilteredComponents(string search, string sortBy, string componentType, decimal? minPrice, decimal? maxPrice)
        {
            var query = _context.Components
                .Include(c => c.ComponentCharacteristics)
                    .ThenInclude(cc => cc.Characteristic)
                .Where(c => c.Quantity > 0)
                .AsQueryable();

            // Поиск по названию и описанию
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    c.Description.ToLower().Contains(search) ||
                    c.Specifications.ToLower().Contains(search)
                );
            }

            // Фильтр по типу комплектующих
            if (!string.IsNullOrEmpty(componentType) && componentType != "all")
            {
                query = query.Where(c => c.Type == componentType);
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
                _ => query.OrderBy(c => c.Id)
            };

            return await query.ToListAsync();
        }


        [AllowAnonymous]
        public async Task<IActionResult> ComputerDetails(int? id, int page = 1)
        {
            if (id == null) return NotFound();

            var computer = await _context.Computers
                .Include(c => c.ComputerComponents)
                    .ThenInclude(cc => cc.Component)
                        .ThenInclude(comp => comp.ComponentCharacteristics)
                            .ThenInclude(cc => cc.Characteristic)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (computer == null) return NotFound();

            // Получаем типы компонентов текущего компьютера
            var componentTypes = computer.ComputerComponents
                .Select(cc => cc.Component.Type)
                .Distinct()
                .ToList();

            // Получаем рекомендованные компьютеры (исключая текущий)
            var recommendedComputers = await _context.Computers
                .Where(c => c.Quantity > 0 && c.Id != id)
                .Take(6)
                .ToListAsync();

            // Получаем рекомендованные комплектующие тех же типов
            var recommendedComponents = await _context.Components
                .Where(c => c.Quantity > 0 && componentTypes.Contains(c.Type))
                .Take(6)
                .ToListAsync();

            // Передаем через ViewBag
            ViewBag.RecommendedComputers = recommendedComputers;
            ViewBag.RecommendedComponents = recommendedComponents;

            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new Cart()
                : JsonSerializer.Deserialize<Cart>(cartJson);

            ViewBag.CartItemsCount = cart?.TotalItems ?? 0;

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
