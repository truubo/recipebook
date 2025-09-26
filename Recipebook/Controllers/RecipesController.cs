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
            var recipes = await _context.Recipe.ToListAsync();
            foreach (Recipe r in recipes)
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
            return View();
        }

        // POST: Recipes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(Recipe recipe)
        {
            recipe.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (CustomFormValidation.FormValid(ModelState))
            {
                try
                {
                    _context.Add(recipe);
                    await _context.SaveChangesAsync();
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

            return View(recipe);
        }

        // GET: Recipes/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _context.Recipe.FindAsync(id);
            if (recipe == null)
            {
                return NotFound();
            }

            // Author-only guard
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized edit attempt on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            return View(recipe);
        }

        // POST: Recipes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Directions,Description,Private")] Recipe recipe)
        {
            if (id != recipe.Id)
            {
                return NotFound();
            }

            // Ensure ownership persists
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            recipe.AuthorId = userId;

            // Author-only guard against tampering
            var original = await _context.Recipe.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (original == null)
            {
                return NotFound();
            }
            if (original.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized edit POST on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            if (CustomFormValidation.FormValid(ModelState))
            {
                try
                {
                    _context.Update(recipe);
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

            return View(recipe);
        }

        // GET: Recipes/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _context.Recipe
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recipe == null)
            {
                return NotFound();
            }

            // Author-only guard
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
            if (recipe == null)
            {
                return RedirectToAction(nameof(Index));
            }

            // Author-only guard
            if (recipe.AuthorId != userId)
            {
                _logger.LogWarning("Unauthorized delete POST on recipe {RecipeId} by user {UserId}.", id, userId);
                return Forbid();
            }

            try
            {
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

        private bool RecipeExists(int id)
        {
            return _context.Recipe.Any(e => e.Id == id);
        }
    }
}
