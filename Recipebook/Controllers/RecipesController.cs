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
                return NotFound();
            }

            var recipe = await _context.Recipe
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recipe == null)
            {
                return NotFound();
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
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(Recipe recipe)
        {
            recipe.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //if (ModelState.IsValid)
            //{
                _context.Add(recipe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            //}
            return View(recipe);
        }

        // GET: Recipes/Edit/5
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
            return View(recipe);
        }

        // POST: Recipes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Directions,Description,Private")] Recipe recipe)
        {
            if (id != recipe.Id)
            {
                return NotFound();
            }
            recipe.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //if (ModelState.IsValid)
            //{
            try
                {
                    _context.Update(recipe);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecipeExists(recipe.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            //}
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
                _context.Recipe.Remove(recipe);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RecipeExists(int id)
        {
            return _context.Recipe.Any(e => e.Id == id);
        }

        // GET: Recipes/AddIngrediant
        [HttpGet]
        public IActionResult AddIngredient()
        {
            return View();
        }

        // POST: Recipes/AddIngrediant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIngredient([Bind("Name")] Ingredient ingredient)
        {
            // Remove ModelState check for debugging
            _context.Ingredient.Add(ingredient);
            await _context.SaveChangesAsync();
            return RedirectToAction("Ingredients");
        }

        // GET: Recipes/Ingrediants
        public async Task<IActionResult> Ingredients()
        {
            var ingredients = await _context.Ingredient.ToListAsync();
            return View(ingredients);
        }

        // GET: Recipes/UpdateIngredient/{id}
        [HttpGet]
        public async Task<IActionResult> UpdateIngredient(int? id)
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateIngredient(int id, [Bind("Id,Name")] Ingredient ingredient)
        {
            if (id != ingredient.Id)
                return NotFound();

            var existingIngredient = await _context.Ingredient.FindAsync(id);
            if (existingIngredient == null)
                return NotFound();

            try
            {
                // Update only the properties you want
                existingIngredient.Name = ingredient.Name;

                _context.Update(existingIngredient);
                await _context.SaveChangesAsync();

                return RedirectToAction("Ingredients");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Ingredient.Any(e => e.Id == id))
                    return NotFound();

                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteIngredient(int id)
        {
            var ingredient = await _context.Ingredient.FindAsync(id);
            if (ingredient == null)
            {
                return NotFound();
            }

            try
            {
                _context.Ingredient.Remove(ingredient);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Optional: log or show error
                Console.WriteLine(ex.Message);
            }

            return RedirectToAction("Ingredients");
        }


    }
}
