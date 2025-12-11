using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers; // for HeaderNames
using Recipebook.Data;
using Recipebook.Models;
using Recipebook.Services.Interfaces;

namespace Recipebook.Controllers
{
    public class IngredientsController : Controller
    {
        // --------------------------- DEPENDENCIES -------------------------------
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IngredientsController> _logger;
        private readonly ITextNormalizationService _textNormalizer;

        public IngredientsController(ApplicationDbContext context, ILogger<IngredientsController> logger, ITextNormalizationService textNormalizer)
        {
            _context = context;
            _logger = logger;
            _textNormalizer = textNormalizer;
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string? searchString)
        {
            IQueryable<Ingredient> query = _context.Ingredient
                .Where(i => !i.IsArchived);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                // case-insensitive search using EF like
                query = query.Where(i =>
                    EF.Functions.Like(i.Name!, $"%{searchString}%"));
            }

            var ingredients = await query.ToListAsync();

            _logger.LogInformation(
                "{Who} -> /Ingredients/Index | count={Count} search='{Search}'",
                Who(),
                ingredients.Count,
                searchString ?? string.Empty);

            ViewBag.SearchString = searchString;

            return View(ingredients);
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

                if (ingredient.Name != null)
                {
                    ingredient.Name = _textNormalizer.NormalizeIngredientName(ingredient.Name);
                }

                _context.Add(ingredient);
                await _context.SaveChangesAsync();

                _logger.LogInformation("{Who} created ingredient '{Name}' (Id {Id})", Who(), ingredient.Name, ingredient.Id);

                TempData["Success"] = $"Ingredient '{ingredient.Name}' created.";

                var acceptHeader = Request.Headers[HeaderNames.Accept].ToString();
                if (acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    return CreatedAtAction(nameof(Index), new { id = ingredient.Id, name = ingredient.Name }, ingredient);
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

            if (!User.IsInRole("Admin"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Who} -> /Ingredients/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                    return Forbid();
                }
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

            if (!User.IsInRole("Admin"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.Equals(existing.OwnerId, uid, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Who} -> /Ingredients/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                    return Forbid();
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (existing.Name != null)
                    {
                        existing.Name = _textNormalizer.NormalizeIngredientName(ingredient.Name!);
                    }

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

            if (!User.IsInRole("Admin"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Who} -> /Ingredients/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                    return Forbid();
                }
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

            if (!User.IsInRole("Admin"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.Equals(ingredient.OwnerId, uid, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Who} -> /Ingredients/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                    return Forbid();
                }
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
                .Where(i => !i.IsArchived)
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