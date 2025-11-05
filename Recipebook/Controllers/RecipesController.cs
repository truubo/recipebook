// Controllers/RecipesController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recipebook.Data;
using Recipebook.Models;
using Recipebook.Models.ViewModels;
using Recipebook.Services;
using Recipebook.Services.Interfaces;
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
        private readonly ITextNormalizationService _textNormalizer;

        public RecipesController(ApplicationDbContext context, ILogger<RecipesController> logger, ITextNormalizationService textNormalizer)
        {
            _context = context;
            _logger = logger;
            _textNormalizer = textNormalizer;
        }

        // Small helper to pretty-print name lists in logs (e.g., category names)
        private static string JoinNames(IEnumerable<string> names) =>
            "[" + string.Join(", ", names) + "]";

        // --------------------------------- INDEX ---------------------------------
        // GET: Recipes
        public async Task<IActionResult> Index(string? searchString, int? tagId, string? scope)
        {
            // Base query with eager loading
            var query = _context.Recipe
                .Where(r => !r.IsArchived)
                .Include(r => r.CategoryRecipes).ThenInclude(cr => cr.Category)
                .Include(r => r.Favorites) // needed for star state
                .AsQueryable();

            // Apply title search
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(r => r.Title.Contains(searchString) && !r.IsArchived);
            }

            // Apply tag filter
            if (tagId.HasValue)
            {
                int cid = tagId.Value;
                query = query.Where(r => r.CategoryRecipes.Where(cr => !cr.Recipe!.IsArchived).Any(cr => cr.CategoryId == cid));
            }

            // Tabs: all / mine / favorites
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            scope = string.IsNullOrWhiteSpace(scope) ? "all" : scope.ToLowerInvariant();

            if (scope == "mine" && uid != null)
                query = query.Where(r => r.AuthorId == uid && !r.IsArchived);

            if (scope == "favorites" && uid != null)
                query = query.Where(r => _context.Favorites.Any(f => f.UserId == uid && f.RecipeId == r.Id) && !r.IsArchived);

            var recipes = await query.Where(r => !r.IsArchived).ToListAsync();

            // Resolve AuthorEmail for display
            foreach (var r in recipes)
            {
                r.AuthorEmail = (await _context.Users
                    .Where(u => u.Id == r.AuthorId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync()) ?? string.Empty;
            }

            // Actor for logging: email if signed-in, else "anonymous"
            string who = (uid == null)
                ? "anonymous"
                : (await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync()) ?? uid;

            _logger.LogInformation(
                "{Who} -> /Recipes/Index | count={Count} search='{Search}' tagId={TagId} scope={Scope}",
                who, recipes.Count, searchString ?? string.Empty, tagId?.ToString() ?? "null", scope
            );

            // Dropdown + preserve selection
            ViewBag.TagList = new SelectList(
                await _context.Category.OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", tagId
            );

            ViewBag.SearchString = searchString;
            ViewBag.TagId = tagId;
            ViewBag.Scope = scope;

            return View(recipes);
        }




        // -------------------------------- DETAILS --------------------------------
        // GET: Recipes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Details requested with null id.");
                return Redirect("/Error/NotFound");
            }

            var recipe = await _context.Recipe
                .Where(r => !r.IsArchived)
                .Include(r => r.CategoryRecipes)
                    .ThenInclude(cr => cr.Category)
                .Include(r => r.IngredientRecipes)       // <-- Include ingredients
                    .ThenInclude(ir => ir.Ingredient)   // <-- Include ingredient details
                .Include(r => r.Favorites)               // ? ADDED (favorites) - for star state
                .Include(r => r.DirectionsList)
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

            // Current viewer identity (email if possible) for logs
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

            // ?? NEW: Provide the lists for the "Add to List" modal
            // Expects your List entity to have an OwnerId (string) and Name (string)
            // Adjust property names if yours differ.
            IEnumerable<SelectListItem> userLists = Enumerable.Empty<SelectListItem>();
            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(uid))
            {
                userLists = await _context.Lists
                    .AsNoTracking()
                    .Where(l => l.OwnerId == uid && !l.IsArchived)   // <- added !IsArchived filter
                    .OrderBy(l => l.Name)
                    .Select(l => new SelectListItem
                    {
                        Value = l.Id.ToString(),
                        Text = l.Name
                    })
                    .ToListAsync();
            }
            ViewBag.UserLists = userLists;

            _logger.LogInformation(
                "{Who} -> /Recipes/Details/{Id} '{Title}' | author={AuthorEmail} private={Private} categories={Count} {Categories} | listsLoaded={ListCount}",
                who, recipe.Id, recipe.Title, authorEmail, recipe.Private, catNames.Count, "[" + string.Join(", ", catNames) + "]",
                userLists.Count()
            );

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
            ViewBag.AllCategories = new MultiSelectList(_context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name), "Id", "Name");

            // Populate ingredients for dropdowns
            ViewBag.AllIngredients = new SelectList(_context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name), "Id", "Name");

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
            ModelState.Remove("UpdateButtonText");
            ModelState.Remove("Recipe.Directions");

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
                ViewBag.AllCategories = new MultiSelectList(_context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
                ViewBag.AllIngredients = new SelectList(_context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name), "Id", "Name");
                return View(vm);
            }

            // -------------------------- HAPPY PATH: save all --------------------------
            // Use a transaction so Recipe + CategoryRecipe + IngredientRecipe are atomic
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1) Save the recipe to get its generated Id
                vm.Recipe.Title = _textNormalizer.NormalizeRecipeTitle(vm.Recipe.Title);

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

                // ==== CHANGED: include prep/cook times in log ====
                _logger.LogInformation(
                    "{Who} created recipe '{Title}' (Id {Id}) private={Private} categories={Count} {Categories} | prep={Prep} cook={Cook}",
                    who, vm.Recipe.Title, vm.Recipe.Id, vm.Recipe.Private, catNames.Count, "[" + string.Join(", ", catNames) + "]",
                    vm.Recipe.PrepTimeMinutes, vm.Recipe.CookTimeMinutes);
                // =================================================

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
            ViewBag.AllCategories = new MultiSelectList(_context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
            ViewBag.AllIngredients = new SelectList(_context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name), "Id", "Name");
            return View(vm);
        }


        // ---------------------------------- EDIT ---------------------------------
        // GET: Recipes/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var who = uid ?? "anonymous";
            _logger.LogInformation("{Who} -> /Recipes/Edit/{Id}", who, id);

            var recipe = await _context.Recipe
                .Where(r => !r.IsArchived)
                .Include(r => r.CategoryRecipes)
                .Include(r => r.IngredientRecipes)
                .Include(r => r.DirectionsList)
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
                    .Select(ir => {
                        Ingredient? ing = _context.Ingredient.Find(ir.IngredientId);
                        return new IngredientSelectViewModel
                        {
                            IngredientId = ir.IngredientId,
                            IngredientName = ing?.Name!,
                            Quantity = ir.Quantity,
                            Unit = ir.Unit
                        };
                    })
                    .ToList()
            };

            ViewBag.AllCategories = new MultiSelectList(_context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
            ViewBag.AllIngredients = new SelectList(_context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name), "Id", "Name");

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

            // Re-validate after cleaning
            ModelState.Clear();
            TryValidateModel(vm);
            ModelState.Remove("UpdateButtonText");
            ModelState.Remove("Recipe.Directions");
            var formOk = FormValid(ModelState) && ModelState.IsValid;

            if (!formOk)
            {
                var errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .Select(kvp => $"{kvp.Key} => {string.Join(" | ", kvp.Value!.Errors.Select(e => e.ErrorMessage))}")
                    .ToList();

                _logger.LogWarning("Recipe Edit blocked. Errors: {Errors}", string.Join("; ", errors));

                ViewBag.AllCategories = new MultiSelectList(_context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
                ViewBag.AllIngredients = new SelectList(_context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name), "Id", "Name");
                return View(vm);
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingDirections = _context.Direction.Where(d => d.RecipeId == vm.Recipe.Id);
                // Remove all existing directions for the recipe
                _context.Direction.RemoveRange(existingDirections);

                // Update recipe basic info
                vm.Recipe.Title = _textNormalizer.NormalizeRecipeTitle(vm.Recipe.Title);

                _context.Update(vm.Recipe);
                await _context.SaveChangesAsync();

                // Update categories
                var existingCategories = _context.CategoryRecipes.Where(cr => cr.RecipeId == vm.Recipe.Id && !cr.Recipe!.IsArchived);
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

                // Update ingredients
                var existingIngredients = _context.IngredientRecipes.Where(ir => ir.RecipeId == vm.Recipe.Id && !ir.Recipe!.IsArchived);
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

                var catNames = (vm.SelectedCategories ?? Array.Empty<int>())
                    .Distinct()
                    .Join(_context.Category, id => id, c => c.Id, (id, c) => c.Name!)
                    .ToList();

                var who = (await _context.Users
                    .Where(u => u.Id == vm.Recipe.AuthorId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync()) ?? vm.Recipe.AuthorId;

                // ==== CHANGED: include prep/cook times in log ====
                _logger.LogInformation(
                    "{Who} updated recipe '{Title}' (Id {Id}) private={Private} categories={Count} {Categories} | prep={Prep} cook={Cook}",
                    who, vm.Recipe.Title, vm.Recipe.Id, vm.Recipe.Private, catNames.Count, "[" + string.Join(", ", catNames) + "]",
                    vm.Recipe.PrepTimeMinutes, vm.Recipe.CookTimeMinutes);
                // =================================================

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

            ViewBag.AllCategories = new MultiSelectList(_context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name), "Id", "Name", vm.SelectedCategories);
            ViewBag.AllIngredients = new SelectList(_context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name), "Id", "Name");
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
                .Where(r => !r.IsArchived)
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
        // Soft-deletes the recipe and removes join rows
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Include join rows so we can remove them
            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                .Include(r => r.IngredientRecipes)
                .Include(r => r.Favorites)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
                return RedirectToAction(nameof(Index));

            // Author-only guard
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized delete POST on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            try
            {
                // Soft delete
                recipe.IsArchived = true;
                _context.Update(recipe);

                // Remove all join rows
                if (recipe.CategoryRecipes != null && recipe.CategoryRecipes.Any())
                    _context.CategoryRecipes.RemoveRange(recipe.CategoryRecipes);

                if (recipe.IngredientRecipes != null && recipe.IngredientRecipes.Any())
                    _context.IngredientRecipes.RemoveRange(recipe.IngredientRecipes);

                if (recipe.Favorites != null && recipe.Favorites.Any())
                    _context.Favorites.RemoveRange(recipe.Favorites);

                await _context.SaveChangesAsync();

                var who = (await _context.Users.Where(u => u.Id == userId)
                    .Select(u => u.Email).FirstOrDefaultAsync()) ?? userId;

                _logger.LogInformation("{Who} archived recipe '{Title}' (Id {Id}) and removed join rows: Categories={CatCount}, Ingredients={IngCount}, Favorites={FavCount}",
                    who, recipe.Title, recipe.Id,
                    recipe.CategoryRecipes?.Count ?? 0,
                    recipe.IngredientRecipes?.Count ?? 0,
                    recipe.Favorites?.Count ?? 0
                );

                TempData["Success"] = "Recipe deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recipe {RecipeId} by user {UserId}.", id, userId);
                TempData["Error"] = "An error occurred while deleting the recipe.";
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> Copy(int id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var who = uid ?? "anonymous";
            _logger.LogInformation("{Who} -> /Recipes/Copy/{Id}", who, id);

            var recipe = await _context.Recipe
                .Where(r => !r.IsArchived)
                .Include(r => r.CategoryRecipes)
                .Include(r => r.IngredientRecipes)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
            {
                _logger.LogWarning("Recipe {Id} not found for copying", id);
                return NotFound();
            }

            _logger.LogInformation(
                "Preparing copy of recipe '{Title}' (Id {Id}) by user {UserId}. Categories={CategoryCount}, Ingredients={IngredientCount}",
                recipe.Title,
                recipe.Id,
                who,
                recipe.CategoryRecipes.Count,
                recipe.IngredientRecipes.Count
            );

            var vm = new RecipeCreateEditVm
            {
                Recipe = new Recipe
                {
                    Title = recipe.Title + " (Copy)",
                    Description = recipe.Description,
                    Directions = recipe.Directions,
                    Private = true, // default to private
                    PrepTimeMinutes = recipe.PrepTimeMinutes,
                    CookTimeMinutes = recipe.CookTimeMinutes
                },
                SelectedCategories = recipe.CategoryRecipes.Select(cr => cr.CategoryId).ToArray(),
                Ingredients = recipe.IngredientRecipes
                    .Select(ir => new IngredientSelectViewModel
                    {
                        IngredientId = ir.IngredientId,
                        Quantity = ir.Quantity,
                        Unit = ir.Unit
                    })
                    .ToList(),
                IsCopy = true
            };

            ViewBag.AllCategories = new MultiSelectList(
                _context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name),
                "Id",
                "Name",
                vm.SelectedCategories
            );

            ViewBag.AllIngredients = new SelectList(
                _context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name),
                "Id",
                "Name"
            );

            _logger.LogInformation(
                "Recipe copy form initialized for '{Title}' (original Id {Id}), default private={Private}",
                vm.Recipe.Title,
                id,
                vm.Recipe.Private
            );

            return View("Edit", vm); // reuse the Edit view
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Copy(RecipeCreateEditVm vm)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            vm.Recipe.AuthorId = uid ?? string.Empty;
            var who = uid ?? "anonymous";
            _logger.LogInformation("{Who} submitted recipe copy creation", who);

            if (string.IsNullOrWhiteSpace(vm.Recipe.AuthorId))
            {
                _logger.LogWarning("Copy attempt blocked: user not signed in.");
                ModelState.AddModelError("", "You must be signed in to create a recipe copy.");
            }

            vm.Ingredients ??= new List<IngredientSelectViewModel>();
            vm.Ingredients = vm.Ingredients.Where(i => i.IngredientId > 0).ToList();

            ModelState.Clear();
            TryValidateModel(vm);

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .Select(kvp => $"{kvp.Key} => {string.Join(" | ", kvp.Value!.Errors.Select(e => e.ErrorMessage))}")
                    .ToList();

                _logger.LogWarning("Recipe copy creation blocked. Validation errors: {Errors}", string.Join("; ", errors));

                ViewBag.AllCategories = new MultiSelectList(
                    _context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name),
                    "Id",
                    "Name",
                    vm.SelectedCategories
                );

                ViewBag.AllIngredients = new SelectList(
                    _context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name),
                    "Id",
                    "Name"
                );

                return View("Edit", vm);
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Recipe.Add(vm.Recipe);
                await _context.SaveChangesAsync();

                // Log categories and ingredients count early
                _logger.LogInformation(
                    "Creating recipe copy '{Title}' by {Who}. Categories={CategoryCount}, Ingredients={IngredientCount}",
                    vm.Recipe.Title,
                    who,
                    vm.SelectedCategories?.Length ?? 0,
                    vm.Ingredients.Count
                );

                // Categories
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

                // Ingredients
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

                var catNames = (vm.SelectedCategories ?? Array.Empty<int>())
                    .Distinct()
                    .Join(_context.Category, id => id, c => c.Id, (id, c) => c.Name!)
                    .ToList();

                _logger.LogInformation(
                    "{Who} created recipe copy '{Title}' (Id {Id}) private={Private} categories={Count} {Categories}",
                    who,
                    vm.Recipe.Title,
                    vm.Recipe.Id,
                    vm.Recipe.Private,
                    catNames.Count,
                    "[" + string.Join(", ", catNames) + "]"
                );

                TempData["Success"] = "Recipe copy created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                var root = ex.GetBaseException();
                _logger.LogError(ex, "Error copying recipe by {Who}. Root cause: {RootMsg}", who, root.Message);
                TempData["Error"] = "An error occurred while creating the copy.";
            }

            ViewBag.AllCategories = new MultiSelectList(
                _context.Category.Where(c => !c.IsArchived).OrderBy(c => c.Name),
                "Id",
                "Name",
                vm.SelectedCategories
            );

            ViewBag.AllIngredients = new SelectList(
                _context.Ingredient.Where(i => !i.IsArchived).OrderBy(i => i.Name),
                "Id",
                "Name"
            );

            return View("Edit", vm);
        }

        // Utility to check if a recipe exists (used in concurrency handling)
        private bool RecipeExists(int id) => _context.Recipe.Where(r => !r.IsArchived).Any(e => e.Id == id);
    }
}
