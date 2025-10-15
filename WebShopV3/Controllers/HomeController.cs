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
        public async Task<IActionResult> Catalog()
        {
            var computers = await _context.Computers
                .Where(c => c.Quantity > 0)
                .ToListAsync();

            return View(computers);
        }

        [AllowAnonymous]
        public async Task<IActionResult> ComputerDetails(int id)
        {
            var computer = await _context.Computers
                .Include(c => c.ComputerComponents)
                .ThenInclude(cc => cc.Component)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (computer == null)
            {
                return NotFound();
            }

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
