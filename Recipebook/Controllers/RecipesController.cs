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
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RecipesController> _logger;

        public RecipesController(ApplicationDbContext context, ILogger<RecipesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Recipes
        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                    .ThenInclude(cr => cr.Category)
                .ToListAsync();

            foreach (var r in recipes)
            {
                r.AuthorEmail = await _context.Users
                    .Where(u => u.Id == r.AuthorId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync();
            }
            return View(recipes);
        }

        // GET: Recipes/Details/5
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

            var authorEmail = await _context.Users
                .Where(u => u.Id == recipe.AuthorId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            ViewData["AuthorEmail"] = authorEmail;

            return View(recipe);
        }

        // GET: Recipes/Create
        [Authorize]
        public IActionResult Create()
        {
            ViewBag.AllCategories = new MultiSelectList(_context.Category, "Id", "Name");
            return View();
        }

        // POST: Recipes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(Recipe recipe, int[] selectedCategories)
        {
            recipe.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (FormValid(ModelState))
            {
                try
                {
                    _context.Add(recipe);
                    await _context.SaveChangesAsync();

                    // link any selected categories
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

                    _logger.LogInformation("Recipe {RecipeId} created by user {UserId}.", recipe.Id, recipe.AuthorId);
                    TempData["Success"] = "Recipe created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating recipe by user {UserId}.", recipe.AuthorId);
                    TempData["Error"] = "An error occurred while creating the recipe.";
                }
            }

            // re-populate categories when validation fails
            ViewBag.AllCategories = new MultiSelectList(_context.Category, "Id", "Name", selectedCategories);
            return View(recipe);
        }

        // GET: Recipes/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null) return NotFound();

            // author-only guard
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized edit attempt on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            // categories with current selections
            var allCategories = await _context.Category.ToListAsync();
            ViewBag.AllCategories = new MultiSelectList(
                allCategories, "Id", "Name",
                recipe.CategoryRecipes.Select(cr => cr.CategoryId)
            );

            return View(recipe);
        }

        // POST: Recipes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Directions,Description,Private")] Recipe recipe, int[] selectedCategories)
        {
            if (id != recipe.Id) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            recipe.AuthorId = userId;

            // author-only guard against tampering
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
                    // update main recipe
                    _context.Update(recipe);
                    await _context.SaveChangesAsync();

                    // sync CategoryRecipes
                    var existing = _context.CategoryRecipes
                        .Where(cr => cr.RecipeId == recipe.Id)
                        .ToList();

                    // remove deselected
                    foreach (var link in existing)
                    {
                        if (selectedCategories == null || !selectedCategories.Contains(link.CategoryId))
                            _context.CategoryRecipes.Remove(link);
                    }

                    // add newly selected
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

                    _logger.LogInformation("Recipe {RecipeId} updated by user {UserId}.", recipe.Id, userId);
                    TempData["Success"] = "Recipe updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!RecipeExists(recipe.Id))
                    {
                        _logger.LogWarning("Recipe {RecipeId} disappeared during update.", recipe.Id);
                        return NotFound();
                    }
                    _logger.LogError(ex, "Concurrency error updating recipe {RecipeId} by user {UserId}.", recipe.Id, userId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating recipe {RecipeId} by user {UserId}.", recipe.Id, userId);
                    TempData["Error"] = "An error occurred while updating the recipe.";
                }
            }

            // reload categories if validation fails
            var allCategoriesReload = await _context.Category.ToListAsync();
            ViewBag.AllCategories = new MultiSelectList(
                allCategoriesReload, "Id", "Name", selectedCategories
            );

            return View(recipe);
        }

        // GET: Recipes/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                    .ThenInclude(cr => cr.Category)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recipe == null) return NotFound();

            // author-only guard
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized delete GET on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            return View(recipe);
        }

        // POST: Recipes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var recipe = await _context.Recipe.FindAsync(id);
            if (recipe == null) return RedirectToAction(nameof(Index));

            // author-only guard
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized delete POST on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            try
            {
                // remove links first
                var categoryLinks = _context.CategoryRecipes.Where(cr => cr.RecipeId == id);
                _context.CategoryRecipes.RemoveRange(categoryLinks);

                _context.Recipe.Remove(recipe);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Recipe {RecipeId} deleted by user {UserId}.", id, userId);
                TempData["Success"] = "Recipe deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recipe {RecipeId} by user {UserId}.", id, userId);
                TempData["Error"] = "An error occurred while deleting the recipe.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool RecipeExists(int id) => _context.Recipe.Any(e => e.Id == id);
    }
}
