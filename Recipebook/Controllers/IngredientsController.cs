// Controllers/IngredientsController.cs
// ----------------------------------------------------------------------------------
// PURPOSE
//   CRUD controller for Ingredient entities. Ingredients can be owned by a user.
//   Includes owner-only guards, logging, and search functionality.
//
// NOTES FOR REVIEWERS / CLASSMATES
//   • Patterns: standard MVC CRUD, ModelState validation, TempData alerts,
//     EF Core queries, Identity-based ownership checks, structured logs.
//   • Identity integration: Ingredient has an OwnerId, checked on Edit/Delete.
// ----------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Security.Claims; // for ClaimTypes
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // logger
using Microsoft.Net.Http.Headers; // for HeaderNames
using Recipebook.Data;
using Recipebook.Models;

namespace Recipebook.Controllers
{
    public class IngredientsController : Controller
    {
        // --------------------------- DEPENDENCIES -------------------------------
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IngredientsController> _logger;

        public IngredientsController(ApplicationDbContext context, ILogger<IngredientsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // --------------------------- UTILITY HELPERS ----------------------------
        private string Who()
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrWhiteSpace(email) ? email : (!string.IsNullOrWhiteSpace(uid) ? uid : "anonymous");
        }

        // -------------------------------- INDEX ---------------------------------
        // GET: Ingredients
        // Supports optional search (?searchString=...)
        public async Task<IActionResult> Index(string? searchString)
        {
            var query = _context.Ingredient.Where(i => !i.IsArchived).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(i => i.Name.Contains(searchString) && !i.IsArchived);
            }

            var ingredients = await query.Where(i => !i.IsArchived).ToListAsync();

            _logger.LogInformation("{Who} -> /Ingredients/Index | count={Count} search='{Search}'",
                Who(), ingredients.Count, searchString ?? string.Empty);

            ViewBag.SearchString = searchString;

            return View(ingredients);
        }

        // -------------------------------- DETAILS --------------------------------
        // GET: Ingredients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Details (null id)", Who());
                return NotFound();
            }

            var ingredient = await _context.Ingredient.Where(i => !i.IsArchived).FirstOrDefaultAsync(i => i.Id == id);

            if (ingredient == null)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Details/{Id} | not found", Who(), id);
                return NotFound();
            }

            _logger.LogInformation("{Who} -> /Ingredients/Details/{Id} '{Name}'",
                Who(), ingredient.Id, ingredient.Name);

            return View(ingredient);
        }

        // -------------------------------- CREATE --------------------------------
        // GET: Ingredients/Create
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Ingredients/Create
        [HttpPost]
        //[Authorize]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] Ingredient ingredient)
        {
            if (ModelState.IsValid)
            {
                ingredient.OwnerId = string.Empty;

                _context.Add(ingredient);
                await _context.SaveChangesAsync();

                _logger.LogInformation("{Who} created ingredient '{Name}' (Id {Id})", Who(), ingredient.Name, ingredient.Id);

                TempData["Success"] = $"Ingredient '{ingredient.Name}' created.";

                var acceptHeader = Request.Headers[HeaderNames.Accept].ToString();
                if (acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    return CreatedAtAction(nameof(Details), new { id = ingredient.Id, name = ingredient.Name }, ingredient);
                }
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("{Who} -> /Ingredients/Create | validation failed ({Errors} errors)", Who(), ModelState.ErrorCount);
            TempData["Error"] = "Please fix the errors and try again.";
            return View(ingredient);
        }

        // ---------------------------------- EDIT --------------------------------
        // GET: Ingredients/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Edit (null id)", Who());
                return NotFound();
            }

            var ingredient = await _context.Ingredient.FindAsync(id);
            if (ingredient == null || ingredient.IsArchived)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Edit/{Id} | not found", Who(), id);
                return NotFound();
            }

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Ingredients/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            _logger.LogInformation("{Who} -> /Ingredients/Edit/{Id} '{Name}'", Who(), ingredient.Id, ingredient.Name);
            return View(ingredient);
        }

        // POST: Ingredients/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Ingredient ingredient)
        {
            if (id != ingredient.Id)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Edit/{Id} | route/body mismatch", Who(), id);
                TempData["Warning"] = "Route/body mismatch.";
                return NotFound();
            }

            var existing = await _context.Ingredient.Where(i => !i.IsArchived).FirstOrDefaultAsync(i => i.Id == id);
            if (existing == null)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Edit/{Id} | disappeared during edit", Who(), id);
                return NotFound();
            }

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
                    existing.Name = ingredient.Name;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("{Who} updated ingredient (Id {Id}) -> '{Name}'", Who(), existing.Id, existing.Name);

                    TempData["Success"] = $"Ingredient '{existing.Name}' updated.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!IngredientExists(existing.Id))
                    {
                        _logger.LogInformation("{Who} -> /Ingredients/Edit/{Id} | disappeared during update", Who(), existing.Id);
                        TempData["Warning"] = "Ingredient no longer exists.";
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError("Concurrency error updating ingredient (Id {Id}) by {Who}", existing.Id, Who());
                        TempData["Error"] = "A concurrency error occurred while updating the ingredient.";
                        throw;
                    }
                }
            }

            _logger.LogInformation("{Who} -> /Ingredients/Edit/{Id} | validation failed ({Errors} errors)", Who(), id, ModelState.ErrorCount);
            TempData["Error"] = "Please fix the errors and try again.";
            return View(existing);
        }

        // --------------------------------- DELETE --------------------------------
        // GET: Ingredients/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Delete (null id)", Who());
                return NotFound();
            }

            var ingredient = await _context.Ingredient.Where(i => !i.IsArchived).FirstOrDefaultAsync(i => i.Id == id);
            if (ingredient == null)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Delete/{Id} | not found", Who(), id);
                return NotFound();
            }

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Ingredients/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            _logger.LogInformation("{Who} -> /Ingredients/Delete/{Id} '{Name}'", Who(), ingredient.Id, ingredient.Name);
            return View(ingredient);
        }

        // POST: Ingredients/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ingredient = await _context.Ingredient
                .Include(i => i.IngredientRecipes) // include join rows
                .FirstOrDefaultAsync(i => i.Id == id);

            if (ingredient == null || ingredient.IsArchived)
            {
                _logger.LogInformation("{Who} -> /Ingredients/Delete/{Id} | already gone", Who(), id);
                TempData["Warning"] = "This ingredient no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Ingredients/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            // Soft-delete the ingredient
            ingredient.IsArchived = true;
            _context.Update(ingredient);

            // Remove join rows so recipes no longer reference this ingredient
            if (ingredient.IngredientRecipes != null && ingredient.IngredientRecipes.Any())
            {
                _context.IngredientRecipes.RemoveRange(ingredient.IngredientRecipes);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("{Who} archived ingredient '{Name}' (Id {Id}) and removed {Count} join rows",
                Who(), ingredient.Name, ingredient.Id, ingredient.IngredientRecipes?.Count ?? 0);

            TempData["Success"] = $"Ingredient '{ingredient.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Ingredients/All
        // API endpoint. Retrieves all ingredients and returns them as JSON.
        public async Task<IActionResult> All()
        {
            var ingredients = await _context.Ingredient
                .Select(i => new { i.Id, i.Name })
                .ToListAsync();
            return Json(ingredients);
        }

        // --------------------------- EXISTENCE CHECK ---------------------------
        private bool IngredientExists(int id)
        {
            return _context.Ingredient.Where(i => !i.IsArchived).Any(e => e.Id == id);
        }
    }
}