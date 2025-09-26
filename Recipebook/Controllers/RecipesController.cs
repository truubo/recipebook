using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Recipebook.Data;
using Recipebook.Models;

namespace Recipebook.Controllers
{
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecipesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Recipes
        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                .ThenInclude(cr => cr.Category)
                .ToListAsync();

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
            if (id == null) return NotFound();

            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                    .ThenInclude(cr => cr.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (recipe == null) return NotFound();

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

            if (!ModelState.IsValid)
            {
                _context.Add(recipe);
                await _context.SaveChangesAsync();

                if (selectedCategories != null)
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

                return RedirectToAction(nameof(Index));
            }

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

            // Pass categories to view; preselect current ones
            var allCategories = await _context.Category.ToListAsync();
            ViewBag.AllCategories = new MultiSelectList(
                allCategories,
                "Id",
                "Name",
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

            recipe.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!ModelState.IsValid)
            {
                try
                {
                    // Update recipe info
                    _context.Update(recipe);
                    await _context.SaveChangesAsync();

                    // Update CategoryRecipe junction table
                    var existingCategories = _context.CategoryRecipes
                        .Where(cr => cr.RecipeId == recipe.Id)
                        .ToList();

                    // Remove deselected categories
                    foreach (var cat in existingCategories)
                    {
                        if (!selectedCategories.Contains(cat.CategoryId))
                            _context.CategoryRecipes.Remove(cat);
                    }

                    // Add newly selected categories
                    foreach (var catId in selectedCategories)
                    {
                        if (!existingCategories.Any(ec => ec.CategoryId == catId))
                        {
                            _context.CategoryRecipes.Add(new CategoryRecipe
                            {
                                RecipeId = recipe.Id,
                                CategoryId = catId
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecipeExists(recipe.Id)) return NotFound();
                    else throw;
                }

                return RedirectToAction(nameof(Index));
            }

            // Reload categories if validation fails
            var allCategoriesReload = await _context.Category.ToListAsync();
            ViewBag.AllCategories = new MultiSelectList(
                allCategoriesReload,
                "Id",
                "Name",
                selectedCategories
            );

            return View(recipe);
        }

        // GET: Recipes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _context.Recipe
                .Include(r => r.CategoryRecipes)
                    .ThenInclude(cr => cr.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            return View(recipe);
        }

        // POST: Recipes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recipe = await _context.Recipe.FindAsync(id);
            if (recipe != null)
            {
                // Remove associated CategoryRecipes first
                var categoryLinks = _context.CategoryRecipes.Where(cr => cr.RecipeId == id);
                _context.CategoryRecipes.RemoveRange(categoryLinks);

                _context.Recipe.Remove(recipe);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RecipeExists(int id)
        {
            return _context.Recipe.Any(e => e.Id == id);
        }
    }
}