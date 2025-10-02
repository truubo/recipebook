using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Recipebook.Data;
using Recipebook.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Recipebook.Controllers
{
    public class IngredientsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CategoriesController> _logger; // logging

        public IngredientsController(ApplicationDbContext context, ILogger<CategoriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private string Who()
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrWhiteSpace(email) ? email : (!string.IsNullOrWhiteSpace(uid) ? uid : "anonymous");
        }

        // GET: Ingredients
        public async Task<IActionResult> Index()
        {
            var ingredients = await _context.Ingredient.ToListAsync();

            _logger.LogInformation("{Who} -> /Ingredients/Index | count={Count}", Who(), ingredients.Count);

            ViewBag.OwnerEmails = await _context.Users
                .Where(u => ingredients.Select(c => c.OwnerId).Distinct().Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email);
            return View(ingredients);
        }

        // GET: Ingredients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Details (null id)", Who());
                return NotFound();
            }

            var ingredient = await _context.Ingredient
                .Include(i => i.IngredientRecipes)
                    .ThenInclude(ir => ir.Recipe)
                .FirstOrDefaultAsync(m => m.Id == id);


            if (ingredient == null)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Details/{Id} | not found", Who(), id);
                return NotFound();
            }

            // Collect recipe titles for readable logs
            var recipeTitles = ingredient.IngredientRecipes
                .Select(ir => ir.Recipe?.Title)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            _logger.LogInformation("{Who} -> /Ingredients/Details/{Id} '{Name}' | recipes={Count} [{Titles}]",
                Who(), ingredient.Id, ingredient.Name, recipeTitles.Count, string.Join(", ", recipeTitles));

            return View(ingredient);
        }

        // GET: Ingredients/Create
        public IActionResult Create()
        {
            _logger.LogInformation("{Who} -> /Ingredients/Create", Who());
            return View();
        }

        // POST: Ingredients/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] Ingredient ingredient)
        {
            if (ModelState.IsValid)
            {
                _context.Add(ingredient);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(ingredient);
        }

        // GET: Ingredients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ingredient = await _context.Ingredient.FindAsync(id);
            if (ingredient == null)
            {
                return NotFound();
            }
            return View(ingredient);
        }

        // POST: Ingredients/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Ingredient ingredient)
        {
            if (id != ingredient.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ingredient);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!IngredientExists(ingredient.Id))
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
            return View(ingredient);
        }

        // GET: Ingredients/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ingredient = await _context.Ingredient
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ingredient == null)
            {
                return NotFound();
            }

            return View(ingredient);
        }

        // POST: Ingredients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ingredient = await _context.Ingredient.FindAsync(id);
            if (ingredient != null)
            {
                _context.Ingredient.Remove(ingredient);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool IngredientExists(int id)
        {
            return _context.Ingredient.Any(e => e.Id == id);
        }
    }
}
