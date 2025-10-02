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
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                ingredient.OwnerId = uid;

                _context.Add(ingredient);
                await _context.SaveChangesAsync();

                _logger.LogInformation("{Who} created ingredient '{Name}' (Id {Id})", Who(), ingredient.Name, ingredient.Id);

                TempData["Success"] = $"Ingredient '{ingredient.Name}' created.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("{Who} -> /Ingredients/Create | validation failed ({Errors} errors)", Who(), ModelState.ErrorCount);
            TempData["Error"] = "Please fix the errors and try again.";
            return View(ingredient);
        }


        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var ingredient = await _context.Ingredient.FindAsync(id);
            if (ingredient == null) return NotFound();

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Ingredients/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            return View(ingredient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Ingredient ingredient)
        {
            if (id != ingredient.Id) return NotFound();

            var existing = await _context.Ingredient.FirstOrDefaultAsync(i => i.Id == id);
            if (existing == null) return NotFound();

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(existing.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Ingredients/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existing.Name = ingredient.Name; // only allow editing name
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("{Who} updated ingredient (Id {Id}) -> '{Name}'", Who(), existing.Id, existing.Name);

                    TempData["Success"] = $"Ingredient '{existing.Name}' updated.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!IngredientExists(existing.Id)) return NotFound();
                    throw;
                }
            }

            return View(existing);
        }


        // GET: Ingredients/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var ingredient = await _context.Ingredient.FirstOrDefaultAsync(m => m.Id == id);
            if (ingredient == null) return NotFound();

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Ingredients/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            return View(ingredient);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ingredient = await _context.Ingredient.FindAsync(id);
            if (ingredient == null) return RedirectToAction(nameof(Index));

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Ingredients/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            _context.Ingredient.Remove(ingredient);
            await _context.SaveChangesAsync();

            _logger.LogInformation("{Who} deleted ingredient '{Name}' (Id {Id})", Who(), ingredient.Name, ingredient.Id);
            TempData["Success"] = $"Ingredient '{ingredient.Name}' deleted.";

            return RedirectToAction(nameof(Index));
        }

        //public async Task<IActionResult> Index(string? searchString)
        //{
        //    var query = _context.Ingredient.AsQueryable();

        //    if (!string.IsNullOrWhiteSpace(searchString))
        //    {
        //        query = query.Where(i => i.Name.Contains(searchString));
        //    }

        //    var ingredients = await query.ToListAsync();

        //    _logger.LogInformation("{Who} -> /Ingredients/Index | count={Count} search='{Search}'",
        //        Who(), ingredients.Count, searchString ?? string.Empty);

        //    var ownerIds = ingredients.Select(i => i.OwnerId).Distinct().ToList();
        //    ViewBag.OwnerEmails = await _context.Users
        //        .Where(u => ownerIds.Contains(u.Id))
        //        .ToDictionaryAsync(u => u.Id, u => u.Email);

        //    ViewBag.SearchString = searchString;

        //    return View(ingredients);
        //}


        private bool IngredientExists(int id)
        {
            return _context.Ingredient.Any(e => e.Id == id);
        }
    }
}
