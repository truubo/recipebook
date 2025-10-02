// Controllers/CategoriesController.cs
// ----------------------------------------------------------------------------------
// PURPOSE
//   CRUD controller for Category entities. Categories can be owned by a user and
//   linked to Recipes via CategoryRecipe. Includes owner-only guards and logging.
//
// NOTES FOR REVIEWERS / CLASSMATES
//   • Patterns: standard MVC CRUD, ModelState validation, TempData alerts,
//     EF Core eager loading, Identity-based ownership checks, structured logs.
//   • Identity integration: Category has an OwnerId, checked on Edit/Delete.
// ----------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Security.Claims; // for ClaimTypes
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // logger
using Recipebook.Data;
using Recipebook.Models;

namespace Recipebook.Controllers
{
    public class CategoriesController : Controller
    {
        // --------------------------- DEPENDENCIES -------------------------------
        private readonly ApplicationDbContext _context; // EF Core DbContext
        private readonly ILogger<CategoriesController> _logger; // logging

        public CategoriesController(ApplicationDbContext context, ILogger<CategoriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // --------------------------- UTILITY HELPERS ----------------------------
        // Quick helper to log "who" is acting: prefer email, then Id, else anonymous
        private string Who()
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrWhiteSpace(email) ? email : (!string.IsNullOrWhiteSpace(uid) ? uid : "anonymous");
        }

        // -------------------------------- INDEX ---------------------------------
        // GET: Categories
        // Shows all categories. Also maps OwnerId -> Email for display.
        // Adds optional search by category name (?searchString=...)
        public async Task<IActionResult> Index(string? searchString)
        {
            var query = _context.Category.AsQueryable();

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(c => c.Name.Contains(searchString));
            }

            var categories = await query.ToListAsync();

            _logger.LogInformation("{Who} -> /Categories/Index | count={Count} search='{Search}'",
                Who(), categories.Count, searchString ?? string.Empty);

            // Preload owner emails for view display
            var ownerIds = categories.Select(c => c.OwnerId).Distinct().ToList();
            ViewBag.OwnerEmails = await _context.Users
                .Where(u => ownerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email);

            // Preserve current search term for sticky input in the view
            ViewBag.SearchString = searchString;

            return View(categories);
        }


        // ------------------------------- DETAILS --------------------------------
        // GET: Categories/Details/5
        // Loads category + linked recipes (via join). Returns NotFound if missing.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Details (null id)", Who());
                return NotFound();
            }

            var category = await _context.Category
                .Include(c => c.CategoryRecipes)
                    .ThenInclude(cr => cr.Recipe)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Details/{Id} | not found", Who(), id);
                return NotFound();
            }

            // Collect recipe titles for readable logs
            var recipeTitles = category.CategoryRecipes
                .Select(cr => cr.Recipe?.Title)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            _logger.LogInformation("{Who} -> /Categories/Details/{Id} '{Name}' | recipes={Count} [{Titles}]",
                Who(), category.Id, category.Name, recipeTitles.Count, string.Join(", ", recipeTitles));

            return View(category);
        }

        // -------------------------------- CREATE --------------------------------
        // GET: Categories/Create
        // Displays empty form.
        [Authorize]
        public IActionResult Create()
        {
            _logger.LogInformation("{Who} -> /Categories/Create", Who());
            return View();
        }

        // POST: Categories/Create
        // Accepts bound Category, stamps OwnerId, saves.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] Category category)
        {
            if (ModelState.IsValid)
            {
                // Server-stamp OwnerId from current user
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                category.OwnerId = uid;

                _context.Add(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("{Who} created category '{Name}' (Id {Id})", Who(), category.Name, category.Id);

                TempData["Success"] = $"Category '{category.Name}' created.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("{Who} -> /Categories/Create | validation failed ({Errors} errors)", Who(), ModelState.ErrorCount);

            TempData["Error"] = "Please fix the errors and try again.";
            return View(category);
        }

        // ---------------------------------- EDIT --------------------------------
        // GET: Categories/Edit/5
        // Loads category for editing. Only owner can edit.
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Edit (null id)", Who());
                return NotFound();
            }

            var category = await _context.Category.FindAsync(id);
            if (category == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Edit/{Id} | not found", Who(), id);
                return NotFound();
            }

            // Owner-only guard
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(category.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Categories/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            _logger.LogInformation("{Who} -> /Categories/Edit/{Id} '{Name}'", Who(), category.Id, category.Name);
            return View(category);
        }

        // POST: Categories/Edit/5
        // Updates allowed fields (Name). Owner-only.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Category category)
        {
            if (id != category.Id)
            {
                _logger.LogInformation("{Who} -> /Categories/Edit/{Id} | route/body mismatch", Who(), id);
                TempData["Warning"] = "Route/body mismatch.";
                return NotFound();
            }

            var existing = await _context.Category.FirstOrDefaultAsync(c => c.Id == id);
            if (existing == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Edit/{Id} | disappeared during edit", Who(), id);
                return NotFound();
            }

            // Owner-only guard
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(existing.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Categories/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existing.Name = category.Name; // only update name
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("{Who} updated category (Id {Id}) -> '{Name}'", Who(), existing.Id, existing.Name);

                    TempData["Success"] = $"Category '{existing.Name}' updated.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(existing.Id))
                    {
                        _logger.LogInformation("{Who} -> /Categories/Edit/{Id} | disappeared during update", Who(), existing.Id);
                        TempData["Warning"] = "Category no longer exists.";
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError("Concurrency error updating category (Id {Id}) by {Who}", existing.Id, Who());
                        TempData["Error"] = "A concurrency error occurred while updating the category.";
                        throw;
                    }
                }
            }

            _logger.LogInformation("{Who} -> /Categories/Edit/{Id} | validation failed ({Errors} errors)", Who(), id, ModelState.ErrorCount);
            TempData["Error"] = "Please fix the errors and try again.";
            return View(existing);
        }

        // --------------------------------- DELETE --------------------------------
        // GET: Categories/Delete/5
        // Shows confirmation. Only owner may delete.
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Delete (null id)", Who());
                return NotFound();
            }

            var category = await _context.Category.FirstOrDefaultAsync(m => m.Id == id);
            if (category == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Delete/{Id} | not found", Who(), id);
                return NotFound();
            }

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(category.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Categories/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            _logger.LogInformation("{Who} -> /Categories/Delete/{Id} '{Name}'", Who(), category.Id, category.Name);
            return View(category);
        }

        // POST: Categories/Delete/5
        // Deletes the category row. Only owner may confirm.
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Category.FindAsync(id);
            if (category == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Delete/{Id} | already gone", Who(), id);
                TempData["Warning"] = "This category no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!string.Equals(category.OwnerId, uid, StringComparison.Ordinal))
            {
                _logger.LogWarning("{Who} -> /Categories/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                return Forbid();
            }

            _context.Category.Remove(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("{Who} deleted category '{Name}' (Id {Id})", Who(), category.Name, category.Id);

            TempData["Success"] = $"Category '{category.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Utility check for existence (used in concurrency handling)
        private bool CategoryExists(int id)
        {
            return _context.Category.Any(e => e.Id == id);
        }
    }
}