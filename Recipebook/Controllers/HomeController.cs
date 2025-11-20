using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipebook.Data;
using Recipebook.Models;
using System.Diagnostics;

namespace Recipebook.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // Load recipes + votes; we'll filter further in the view
            var recipes = _context.Recipe
                .Include(r => r.RecipeVotes)
                .ToList();

            return View(recipes);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
