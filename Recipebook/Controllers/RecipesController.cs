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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recipebook.Data;
using Recipebook.Models;
using Recipebook.Models.ViewModels;
using Recipebook.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
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
        // Adds title search + tag (category) filter while preserving logging and author resolution.
        public async Task<IActionResult> Index(string? searchString, int? tagId)
        {
            // Base query with eager loading (so the view can show category names without extra DB calls)
            var query = _context.Recipe
                .Include(r => r.CategoryRecipes)
                    .ThenInclude(cr => cr.Category)
                .AsQueryable();

            // Apply title search
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(r => r.Title.Contains(searchString));
            }

            // Apply tag filter (CategoryId) if provided
            if (tagId.HasValue)
            {
                int cid = tagId.Value;
                query = query.Where(r => r.CategoryRecipes.Any(cr => cr.CategoryId == cid));
            }

            var recipes = await query.ToListAsync();

            // Resolve AuthorEmail for display
            foreach (var r in recipes)
            {
                r.AuthorEmail = (await _context.Users
                    .Where(u => u.Id == r.AuthorId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync()) ?? string.Empty;
            }

            // Actor for logging: email if signed-in, else "anonymous"
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string who = (uid == null)
                ? "anonymous"
                : (await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync()) ?? uid;

            // Log with filter context
            _logger.LogInformation(
                "{Who} -> /Recipes/Index | count={Count} search='{Search}' tagId={TagId}",
                who, recipes.Count, searchString ?? string.Empty, tagId?.ToString() ?? "null"
            );

            // ? Populate dropdown AND preserve selection
            ViewBag.TagList = new SelectList(
                await _context.Category.OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", tagId
            );

            ViewBag.SearchString = searchString;
            ViewBag.TagId = tagId;

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
                .Include(r => r.IngredientRecipes)       // <-- Include ingredients
                    .ThenInclude(ir => ir.Ingredient)   // <-- Include ingredient details
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
        [Authorize]
        public IActionResult Create()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var who = uid ?? "anonymous";
            _logger.LogInformation("{Who} -> /Recipes/Create", who);

            var vm = new RecipeCreateEditVm();

            // Populate categories for multi-select
            ViewBag.AllCategories = new MultiSelectList(_context.Category.OrderBy(c => c.Name), "Id", "Name");

            // Populate ingredients for dropdowns
            ViewBag.AllIngredients = new SelectList(_context.Ingredient.OrderBy(i => i.Name), "Id", "Name");

            return View(vm);
        }

        // POST: Recipes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(RecipeCreateEditVm vm)
        {
            // --------------------- PREP: stamp author + normalize ---------------------
            vm.Recipe.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(vm.Recipe.AuthorId))
            {
                // Shouldn't happen due to [Authorize], but guard just in case
                ModelState.AddModelError("", "You must be signed in to create a recipe.");
            }

            // Remove any blank ingredient rows (e.g., placeholder UI rows)
            vm.Ingredients ??= new List<IngredientSelectViewModel>();
            vm.Ingredients = vm.Ingredients.Where(i => i.IngredientId > 0).ToList();

            // Re-validate AFTER cleaning the collection so ModelState isn't poisoned
            ModelState.Clear();
            TryValidateModel(vm);

            // Custom form validation (keep your existing checks)
            var formOk = FormValid(ModelState) && ModelState.IsValid;

            if (!formOk)
            {
                // Dump precise reasons to the log to see what's blocking the post
                var errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .Select(kvp => $"{kvp.Key} => {string.Join(" | ", kvp.Value!.Errors.Select(e => e.ErrorMessage))}")
                    .ToList();

                _logger.LogWarning("Recipe Create blocked. Errors: {Errors}", string.Join("; ", errors));

                // Re-populate dropdowns and return form
                ViewBag.AllCategories = new MultiSelectList(_context.Category.OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
                ViewBag.AllIngredients = new SelectList(_context.Ingredient.OrderBy(i => i.Name), "Id", "Name");
                return View(vm);
            }

            // -------------------------- HAPPY PATH: save all --------------------------
            // Use a transaction so Recipe + CategoryRecipe + IngredientRecipe are atomic
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // ----------------------- HANDLE IMAGE UPLOAD -----------------------
                // ----------------------- HANDLE IMAGE UPLOAD -----------------------
                if (vm.ImageFile != null)
                {
                    string imgDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "recipes");
                    Directory.CreateDirectory(imgDir); // ensure folder exists

                    // ✅ Corrected lines:
                    string originalFileName = Path.GetFileNameWithoutExtension(vm.ImageFile.FileName);
                    string ext = Path.GetExtension(vm.ImageFile.FileName);
                    string safeFile = $"{originalFileName}_{Guid.NewGuid()}{ext}";
                    string fullPath = Path.Combine(imgDir, safeFile);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await vm.ImageFile.CopyToAsync(stream);
                    }

                    vm.Recipe.ImageFileName = safeFile;
                }


                // 1) Save the recipe to get its generated Id
                _context.Add(vm.Recipe);

                // Optional: quick visibility into what EF plans to write
                var pending1 = _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    .Select(e => $"{e.Entity.GetType().Name}:{e.State}")
                    .ToList();
                _logger.LogInformation("Before first SaveChanges -> {Pending}", string.Join(", ", pending1));

                await _context.SaveChangesAsync();

                // 2) Add selected categories
                if (vm.SelectedCategories?.Length > 0)
                {
                    foreach (var catId in vm.SelectedCategories.Distinct())
                    {
                        _context.CategoryRecipes.Add(new CategoryRecipe
                        {
                            RecipeId = vm.Recipe.Id,
                            CategoryId = catId
                        });
                    }
                }

                // 3) Add ingredients (already filtered to valid rows)
                foreach (var ingredientVm in vm.Ingredients)
                {
                    _context.IngredientRecipes.Add(new IngredientRecipe
                    {
                        RecipeId = vm.Recipe.Id,
                        IngredientId = ingredientVm.IngredientId,
                        Quantity = ingredientVm.Quantity,
                        Unit = ingredientVm.Unit   // Enum binder OK; EF converts to string per OnModelCreating
                    });
                }

                var pending2 = _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    .Select(e => $"{e.Entity.GetType().Name}:{e.State}")
                    .ToList();
                _logger.LogInformation("Before second SaveChanges -> {Pending}", string.Join(", ", pending2));

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // Logging (friendly)
                var catNames = (vm.SelectedCategories ?? Array.Empty<int>())
                    .Distinct()
                    .Join(_context.Category, id => id, c => c.Id, (id, c) => c.Name!)
                    .ToList();

                var who = (await _context.Users
                    .Where(u => u.Id == vm.Recipe.AuthorId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync()) ?? vm.Recipe.AuthorId;

                _logger.LogInformation(
                    "{Who} created recipe '{Title}' (Id {Id}) private={Private} categories={Count} {Categories}",
                    who, vm.Recipe.Title, vm.Recipe.Id, vm.Recipe.Private, catNames.Count, "[" + string.Join(", ", catNames) + "]");

                TempData["Success"] = "Recipe created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();

                var root = ex.GetBaseException();
                _logger.LogError(ex, "Error creating recipe by user {UserId}. Root: {RootMsg}", vm.Recipe.AuthorId, root.Message);
                TempData["Error"] = "An error occurred while creating the recipe.";
            }

            // ------------------------ FALLBACK: re-show the form ----------------------
            ViewBag.AllCategories = new MultiSelectList(_context.Category.OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
            ViewBag.AllIngredients = new SelectList(_context.Ingredient.OrderBy(i => i.Name), "Id", "Name");
            return View(vm);
        }


        // ---------------------------------- EDIT ---------------------------------
        // GET: Recipes/Edit/5
        // GET: Recipes/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var who = uid ?? "anonymous";
            _logger.LogInformation("{Who} -> /Recipes/Edit/{Id}", who, id);

            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                .Include(r => r.IngredientRecipes)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
            {
                _logger.LogWarning("Recipe {Id} not found for editing", id);
                return NotFound();
            }

            var vm = new RecipeCreateEditVm
            {
                Recipe = recipe,
                SelectedCategories = recipe.CategoryRecipes.Select(cr => cr.CategoryId).ToArray(),
                Ingredients = recipe.IngredientRecipes
                    .Select(ir => new IngredientSelectViewModel
                    {
                        IngredientId = ir.IngredientId,
                        Quantity = ir.Quantity,
                        Unit = ir.Unit
                    })
                    .ToList()
            };

            ViewBag.AllCategories = new MultiSelectList(_context.Category.OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
            ViewBag.AllIngredients = new SelectList(_context.Ingredient.OrderBy(i => i.Name), "Id", "Name");

            return View(vm);
        }

        // POST: Recipes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, RecipeCreateEditVm vm)
        {
            vm.Recipe.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(vm.Recipe.AuthorId))
            {
                ModelState.AddModelError("", "You must be signed in to edit a recipe.");
            }

            // Clean blank ingredient rows
            vm.Ingredients ??= new List<IngredientSelectViewModel>();
            vm.Ingredients = vm.Ingredients.Where(i => i.IngredientId > 0).ToList();

            ModelState.Clear();
            TryValidateModel(vm);
            var formOk = FormValid(ModelState) && ModelState.IsValid;

            if (!formOk)
            {
                var errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .Select(kvp => $"{kvp.Key} => {string.Join(" | ", kvp.Value!.Errors.Select(e => e.ErrorMessage))}")
                    .ToList();

                _logger.LogWarning("Recipe Edit blocked. Errors: {Errors}", string.Join("; ", errors));

                ViewBag.AllCategories = new MultiSelectList(_context.Category.OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
                ViewBag.AllIngredients = new SelectList(_context.Ingredient.OrderBy(i => i.Name), "Id", "Name");
                return View(vm);
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // ----------------------- LOAD EXISTING RECIPE -----------------------
                var existingRecipe = await _context.Recipe.AsNoTracking().FirstOrDefaultAsync(r => r.Id == vm.Recipe.Id);
                if (existingRecipe == null)
                {
                    TempData["Error"] = "Recipe not found.";
                    return RedirectToAction(nameof(Index));
                }

                // ----------------------- HANDLE IMAGE UPLOAD -----------------------
                if (vm.ImageFile != null)
                {
                    string imgDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "recipes");
                    Directory.CreateDirectory(imgDir);

                    // Optional: delete old image if exists
                    if (!string.IsNullOrEmpty(existingRecipe.ImageFileName))
                    {
                        string oldPath = Path.Combine(imgDir, existingRecipe.ImageFileName);
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    string originalFileName = Path.GetFileNameWithoutExtension(vm.ImageFile.FileName);
                    string ext = Path.GetExtension(vm.ImageFile.FileName);
                    string safeFile = $"{originalFileName}_{Guid.NewGuid()}{ext}";
                    string fullPath = Path.Combine(imgDir, safeFile);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await vm.ImageFile.CopyToAsync(stream);
                    }

                    vm.Recipe.ImageFileName = safeFile; // new image
                }
                else
                {
                    // ✅ Keep old image if no new one uploaded
                    vm.Recipe.ImageFileName = existingRecipe.ImageFileName;
                }

                // ----------------------- UPDATE BASIC INFO -----------------------
                _context.Update(vm.Recipe);
                await _context.SaveChangesAsync();

                // ----------------------- UPDATE CATEGORIES -----------------------
                var existingCategories = _context.CategoryRecipes.Where(cr => cr.RecipeId == vm.Recipe.Id);
                _context.CategoryRecipes.RemoveRange(existingCategories);

                if (vm.SelectedCategories?.Length > 0)
                {
                    foreach (var catId in vm.SelectedCategories.Distinct())
                    {
                        _context.CategoryRecipes.Add(new CategoryRecipe
                        {
                            RecipeId = vm.Recipe.Id,
                            CategoryId = catId
                        });
                    }
                }

                // ----------------------- UPDATE INGREDIENTS -----------------------
                var existingIngredients = _context.IngredientRecipes.Where(ir => ir.RecipeId == vm.Recipe.Id);
                _context.IngredientRecipes.RemoveRange(existingIngredients);

                foreach (var ingVm in vm.Ingredients)
                {
                    _context.IngredientRecipes.Add(new IngredientRecipe
                    {
                        RecipeId = vm.Recipe.Id,
                        IngredientId = ingVm.IngredientId,
                        Quantity = ingVm.Quantity,
                        Unit = ingVm.Unit
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = "Recipe updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                var root = ex.GetBaseException();
                _logger.LogError(ex, "Error updating recipe {Id} by user {UserId}. Root: {RootMsg}", vm.Recipe.Id, vm.Recipe.AuthorId, root.Message);
                TempData["Error"] = "An error occurred while updating the recipe.";
            }

            ViewBag.AllCategories = new MultiSelectList(_context.Category.OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
            ViewBag.AllIngredients = new SelectList(_context.Ingredient.OrderBy(i => i.Name), "Id", "Name");
            return View(vm);
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
