using System;
using System.Linq;
using System.Security.Claims; // for ClaimTypes
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recipebook.Data;
using Recipebook.Models;
using Recipebook.Services.Interfaces;

namespace Recipebook.Controllers
{
    public class CategoriesController : Controller
    {
        // --------------------------- DEPENDENCIES -------------------------------
        private readonly ApplicationDbContext _context; // EF Core DbContext
        private readonly ILogger<CategoriesController> _logger; // logging
        private readonly ITextNormalizationService _textNormalizer; // text normalization

        public CategoriesController(ApplicationDbContext context, ILogger<CategoriesController> logger, ITextNormalizationService textNormalizer)
        {
            _context = context;
            _logger = logger;
            _textNormalizer = textNormalizer;
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
            var query = _context.Category.Where(c => !c.IsArchived).AsQueryable();

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(c => c.Name.Contains(searchString) && !c.IsArchived);
            }

            var categories = await query
                .Where(c => !c.IsArchived)
                .Include(c => c.CategoryRecipes)
                    .ThenInclude(cr => cr.Recipe)
                .ToListAsync();

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
                .Where(c => !c.IsArchived)
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
                .Where(cr => !cr.Recipe.IsArchived)
                .Select(cr => cr.Recipe?.Title)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            var ownerEmail = await _context.Users
                .Where(u => u.Id == category.OwnerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            ViewBag.OwnerEmail = ownerEmail;

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

                category.Name = _textNormalizer.NormalizeCategory(category.Name);

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
            if (category == null || category.IsArchived)
            {
                _logger.LogInformation("{Who} -> /Categories/Edit/{Id} | not found", Who(), id);
                return NotFound();
            }

            if (!User.IsInRole("Admin"))
            {
                // Owner-only guard
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.Equals(category.OwnerId, uid, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Who} -> /Categories/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                    return Forbid();
                }
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

            var existing = await _context.Category.Where(c => !c.IsArchived).FirstOrDefaultAsync(c => c.Id == id);
            if (existing == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Edit/{Id} | disappeared during edit", Who(), id);
                return NotFound();
            }

            if (!User.IsInRole("Admin"))
            {
                // Owner-only guard
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.Equals(existing.OwnerId, uid, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Who} -> /Categories/Edit/{Id} | forbidden (owner mismatch)", Who(), id);
                    return Forbid();
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existing.Name = _textNormalizer.NormalizeCategory(category.Name); // only update name
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

            var category = await _context.Category.Where(c => !c.IsArchived).FirstOrDefaultAsync(m => m.Id == id);
            if (category == null)
            {
                _logger.LogInformation("{Who} -> /Categories/Delete/{Id} | not found", Who(), id);
                return NotFound();
            }

            if (!User.IsInRole("Admin"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.Equals(category.OwnerId, uid, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Who} -> /Categories/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                    return Forbid();
                }
            }

            _logger.LogInformation("{Who} -> /Categories/Delete/{Id} '{Name}'", Who(), category.Id, category.Name);
            return View(category);
        }

        // POST: Categories/Delete/5
        // Soft deletes the category by setting IsArchived = true
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Category
                .Include(c => c.CategoryRecipes) // include join rows
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null || category.IsArchived)
            {
                _logger.LogInformation("{Who} -> /Categories/Delete/{Id} | already gone", Who(), id);
                TempData["Warning"] = "This category no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            if (!User.IsInRole("Admin"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.Equals(category.OwnerId, uid, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Who} -> /Categories/Delete/{Id} | forbidden (owner mismatch)", Who(), id);
                    return Forbid();
                }
            }

            // Soft-delete the category
            category.IsArchived = true;
            _context.Update(category);

            // Remove join rows so recipes no longer reference this category
            if (category.CategoryRecipes != null && category.CategoryRecipes.Any())
            {
                _context.CategoryRecipes.RemoveRange(category.CategoryRecipes);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("{Who} archived category '{Name}' (Id {Id}) and removed {Count} join rows",
                Who(), category.Name, category.Id, category.CategoryRecipes?.Count ?? 0);

            TempData["Success"] = $"Category '{category.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Utility check for existence (used in concurrency handling)
        private bool CategoryExists(int id)
        {
            return _context.Category.Where(c => !c.IsArchived).Any(e => e.Id == id);
        }
    }
}