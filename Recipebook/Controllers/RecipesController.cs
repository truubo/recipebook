// Controllers/RecipesController.cs
// ----------------------------------------------------------------------------------
// PURPOSE
//   CRUD controller for Recipe entities. Also includes author-only guards and structured
//   logging for clear audit trails.
//
// NOTES FOR REVIEWERS / CLASSMATES
//   • Look for SECTION HEADERS to navigate (INDEX, DETAILS, CREATE, EDIT, DELETE).
//   • Common patterns used: eager loading (Include/ThenInclude), ModelState validation,
//     PRG (Post/Redirect/Get), TempData alerts, and Identity-based ownership checks.
//   • Controllers orchestrate: validate ? call EF/service ? redirect. Relationship
//     definitions belong in the Model/DbContext; syncing selections to join rows
//     happens here for now (could be moved to a service later to follow SRP).
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recipebook.Data;
using Recipebook.Models;
using Recipebook.Services;
using static Recipebook.Services.CustomFormValidation;

namespace Recipebook.Controllers
{
    // No class-level [Authorize]: Index/Details can be viewed without login.
    public class RecipesController : Controller
    {
        // --------------------------- DEPENDENCIES (DI) ---------------------------
        // _context: EF Core DbContext (DB access)
        // _logger:  ILogger for diagnostic/audit logs
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RecipesController> _logger;

        public RecipesController(ApplicationDbContext context, ILogger<RecipesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Small helper to pretty-print name lists in logs (e.g., category names)
        private static string JoinNames(IEnumerable<string> names) =>
            "[" + string.Join(", ", names) + "]";

        // --------------------------------- INDEX ---------------------------------
        // GET: Recipes
        // Loads recipes with their category links so the view can render category
        // names without extra DB calls. Also resolves AuthorEmail for display.
        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipe
                .Include(r => r.CategoryRecipes)        // eager-load join rows
                    .ThenInclude(cr => cr.Category)     // eager-load Category for names
                .ToListAsync();

            foreach (var r in recipes)
            {
                // CS8601 note: FirstOrDefaultAsync may return null; coalesce to "" to
                // avoid assigning null into a non-nullable property. Behavior unchanged.
                r.AuthorEmail = (await _context.Users
                    .Where(u => u.Id == r.AuthorId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync()) ?? string.Empty;
            }

            // Build a readable actor for the log: use email if signed-in else "anonymous".
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string who = (uid == null)
                ? "anonymous"
                : (await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync()) ?? uid;

            _logger.LogInformation("{Who} -> /Recipes/Index | count={Count}",
                who, recipes.Count);

            return View(recipes);
        }

        // -------------------------------- DETAILS --------------------------------
        // GET: Recipes/Details/5
        // Loads a single recipe and its categories. Redirects to a friendly NotFound
        // page if id is null or the record doesn't exist.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Details requested with null id.");
                return Redirect("/Error/NotFound");
            }

            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                    .ThenInclude(cr => cr.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (recipe == null)
            {
                _logger.LogWarning("Details requested for missing recipe {RecipeId}.", id);
                return Redirect("/Error/NotFound");
            }

            // For the view: resolve author email to display who created it
            var authorEmail = await _context.Users
                .Where(u => u.Id == recipe.AuthorId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            ViewData["AuthorEmail"] = authorEmail;

            // Build actor for logging (email if possible)
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string who = (uid == null)
                ? "anonymous"
                : (await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync()) ?? uid;

            // List category names for a readable log line
            var catNames = recipe.CategoryRecipes
                .Select(cr => cr.Category?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .ToList();

            _logger.LogInformation(
                "{Who} -> /Recipes/Details/{Id} '{Title}' | author={AuthorEmail} private={Private} categories={Count} {Categories}",
                who, recipe.Id, recipe.Title, authorEmail, recipe.Private, catNames.Count, JoinNames(catNames));

            return View(recipe);
        }

        // -------------------------------- CREATE ---------------------------------
        // GET: Recipes/Create
        // Shows the blank form. Requires authentication.
        [Authorize]
        public IActionResult Create()
        {
            // Populate the category multiselect (none preselected on GET)
            ViewBag.AllCategories = new MultiSelectList(_context.Category, "Id", "Name");

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var who = uid ?? "anonymous";
            _logger.LogInformation("{Who} -> /Recipes/Create", who);

            return View();
        }

        // POST: Recipes/Create
        // Accepts bound Recipe and array of selected category IDs. Creates the recipe
        // then creates join rows in CategoryRecipe for any selections.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(Recipe recipe, int[] selectedCategories)
        {
            // Stamp the author from the signed-in user. Fallback "" avoids null assignment.
            recipe.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (FormValid(ModelState))
            {
                try
                {
                    // 1) Insert the recipe so we have its generated Id
                    _context.Add(recipe);
                    await _context.SaveChangesAsync();

                    // 2) Add join rows for any selected categories
                    if (selectedCategories != null && selectedCategories.Length > 0)
                    {
                        foreach (var catId in selectedCategories)
                        {
                            _context.CategoryRecipes.Add(new CategoryRecipe
                            {
                                RecipeId = recipe.Id,
                                CategoryId = catId
                            });
                        }
                        await _context.SaveChangesAsync();
                    }

                    // For logging: resolve category names for a readable summary
                    var catNames = (selectedCategories ?? Array.Empty<int>())
                        .Distinct()
                        .Join(_context.Category, id => id, c => c.Id, (id, c) => c.Name!)
                        .ToList();

                    var uid = recipe.AuthorId;
                    var who = (await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync()) ?? uid;

                    _logger.LogInformation(
                        "{Who} created recipe '{Title}' (Id {Id}) private={Private} categories={Count} {Categories}",
                        who, recipe.Title, recipe.Id, recipe.Private, catNames.Count, JoinNames(catNames));

                    TempData["Success"] = "Recipe created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Log the exception (message + stack) but show a friendly alert to users
                    _logger.LogError(ex, "Error creating recipe by user {UserId}.", recipe.AuthorId);
                    TempData["Error"] = "An error occurred while creating the recipe.";
                }
            }

            // If validation fails or an exception occurred, we must re-populate the multiselect
            ViewBag.AllCategories = new MultiSelectList(_context.Category, "Id", "Name", selectedCategories);
            return View(recipe);
        }

        // ---------------------------------- EDIT ---------------------------------
        // GET: Recipes/Edit/5
        // Loads the recipe with its current category links. Only the author may edit.
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null) return NotFound();

            // Author-only guard (server-side check)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty; // CS8601 fix
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized edit attempt on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            // Build the category multiselect, preselecting the currently linked categories
            var allCategories = await _context.Category.ToListAsync();
            ViewBag.AllCategories = new MultiSelectList(
                allCategories, "Id", "Name",
                recipe.CategoryRecipes.Select(cr => cr.CategoryId)
            );

            var who = (await _context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync()) ?? userId;
            _logger.LogInformation("{Who} -> /Recipes/Edit/{Id} '{Title}' | selected={Count}",
                who, recipe.Id, recipe.Title, recipe.CategoryRecipes.Count);

            return View(recipe);
        }

        // POST: Recipes/Edit/5
        // Updates scalar fields and synchronizes the CategoryRecipe join rows, keeping
        // only the selections provided by the user.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Directions,Description,Private")] Recipe recipe, int[] selectedCategories)
        {
            if (id != recipe.Id) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty; // CS8601 fix
            recipe.AuthorId = userId; // Server owns AuthorId; never trust client on this

            // Author-only guard against tampering: compare with original row
            var original = await _context.Recipe.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (original == null) return NotFound();
            if (original.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized edit POST on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            if (FormValid(ModelState))
            {
                try
                {
                    // Update the main Recipe row
                    _context.Update(recipe);
                    await _context.SaveChangesAsync();

                    // Sync the join table CategoryRecipes to match the selections
                    var existing = _context.CategoryRecipes
                        .Where(cr => cr.RecipeId == recipe.Id)
                        .ToList();

                    // Remove any links that are no longer selected
                    foreach (var link in existing)
                    {
                        if (selectedCategories == null || !selectedCategories.Contains(link.CategoryId))
                            _context.CategoryRecipes.Remove(link);
                    }

                    // Add links for any newly selected categories not already present
                    if (selectedCategories != null)
                    {
                        foreach (var catId in selectedCategories)
                        {
                            if (!existing.Any(ec => ec.CategoryId == catId))
                            {
                                _context.CategoryRecipes.Add(new CategoryRecipe
                                {
                                    RecipeId = recipe.Id,
                                    CategoryId = catId
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();

                    // For logging: compute added/removed names (optional, for readability)
                    var addedIds = (selectedCategories ?? Array.Empty<int>()).Except(existing.Select(e => e.CategoryId)).ToArray();
                    var removedIds = existing.Select(e => e.CategoryId).Except(selectedCategories ?? Array.Empty<int>()).ToArray();

                    var addedTitles = addedIds.Length == 0
                        ? new List<string>()
                        : await _context.Category.Where(c => addedIds.Contains(c.Id)).Select(c => c.Name!).ToListAsync();
                    var removedTitles = removedIds.Length == 0
                        ? new List<string>()
                        : await _context.Category.Where(c => removedIds.Contains(c.Id)).Select(c => c.Name!).ToListAsync();

                    var who = (await _context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync()) ?? userId;
                    _logger.LogInformation(
                        "{Who} updated recipe '{Title}' (Id {Id}) | added={Added} {AddedTitles} removed={Removed} {RemovedTitles}",
                        who, recipe.Title, recipe.Id,
                        addedTitles.Count, JoinNames(addedTitles),
                        removedTitles.Count, JoinNames(removedTitles));

                    TempData["Success"] = "Recipe updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // If the row disappeared between fetch and save
                    if (!RecipeExists(recipe.Id))
                    {
                        _logger.LogWarning("Recipe {RecipeId} disappeared during update.", recipe.Id);
                        return NotFound();
                    }
                    _logger.LogError(ex, "Concurrency error updating recipe {RecipeId} by user {UserId}.", recipe.Id, userId);
                    throw; // let middleware show the developer page in Development
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating recipe {RecipeId} by user {UserId}.", recipe.Id, userId);
                    TempData["Error"] = "An error occurred while updating the recipe.";
                }
            }

            // If invalid, rebuild multiselect so user selections persist on redisplay
            var allCategoriesReload = await _context.Category.ToListAsync();
            ViewBag.AllCategories = new MultiSelectList(
                allCategoriesReload, "Id", "Name", selectedCategories
            );

            return View(recipe);
        }

        // --------------------------------- DELETE --------------------------------
        // GET: Recipes/Delete/5
        // Shows a confirmation page. Only the author may delete.
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                    .ThenInclude(cr => cr.Category)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recipe == null) return NotFound();

            // Author-only guard
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty; // CS8601 fix
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized delete GET on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            var who = (await _context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync()) ?? userId;
            _logger.LogInformation("{Who} -> /Recipes/Delete/{Id} '{Title}'", who, recipe.Id, recipe.Title);

            return View(recipe);
        }

        // POST: Recipes/Delete/5
        // Performs the actual delete (after confirmation) and redirects to Index.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty; // CS8601 fix

            var recipe = await _context.Recipe.FindAsync(id);
            if (recipe == null) return RedirectToAction(nameof(Index));

            // Author-only guard
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized delete POST on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            try
            {
                // Remove join rows first to avoid orphaned links (if cascade not set)
                var categoryLinks = _context.CategoryRecipes.Where(cr => cr.RecipeId == id);
                _context.CategoryRecipes.RemoveRange(categoryLinks);

                _context.Recipe.Remove(recipe);
                await _context.SaveChangesAsync();

                var who = (await _context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync()) ?? userId;
                _logger.LogInformation("{Who} deleted recipe '{Title}' (Id {Id})", who, recipe.Title, recipe.Id);

                TempData["Success"] = "Recipe deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recipe {RecipeId} by user {UserId}.", id, userId);
                TempData["Error"] = "An error occurred while deleting the recipe.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Utility to check if a recipe exists (used in concurrency handling)
        private bool RecipeExists(int id) => _context.Recipe.Any(e => e.Id == id);
    }
}
