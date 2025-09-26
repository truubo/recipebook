// Controllers/ListsController.cs

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;             // ← added
using Recipebook.Data;
using Recipebook.Models;
using Recipebook.Models.ViewModels;

namespace Recipebook.Controllers
{
    [Authorize]
    public class ListsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ListsController> _logger;   // ← added

        // ctor now accepts a logger
        public ListsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<ListsController> logger)                 // ← added
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;                                 // ← added
        }

        // Helper to build recipe dropdowns
        private async Task<List<SelectListItem>> BuildRecipeChoicesAsync(string currentUserId, int[]? preselect = null)
        {
            var recipes = await _context.Recipe
                .AsNoTracking()
                .Where(r => r.AuthorId == currentUserId || r.Private == false)
                .OrderBy(r => r.Title)
                .Select(r => new { r.Id, r.Title })
                .ToListAsync();

            var selected = new HashSet<int>(preselect ?? Array.Empty<int>());

            return recipes.Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.Title,
                Selected = selected.Contains(r.Id)
            }).ToList();
        }

        // GET: Lists
        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User)!;

            // My lists
            var myLists = await _context.Lists
                .Where(l => l.OwnerId == uid)
                .Include(l => l.ListRecipes)
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .ToListAsync();

            // All lists visible: mine OR public
            var allLists = await _context.Lists
                .Where(l => l.OwnerId == uid || l.Private == false)
                .Include(l => l.ListRecipes)
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .ToListAsync();

            // Build ownerId -> email dictionary
            var ownerIds = myLists.Select(l => l.OwnerId)
                .Concat(allLists.Select(l => l.OwnerId))
                .Distinct()
                .ToList();

            var ownerEmails = await _context.Users
                .Where(u => ownerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email);

            ViewBag.OwnerEmails = ownerEmails;

            var myEmail = await _context.Users
                .Where(u => u.Id == uid)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var vm = new ListsIndexVm
            {
                MyLists = myLists,
                AllLists = allLists,
                MyEmail = myEmail,
                MyUserId = uid
            };

            return View(vm);
        }

        // GET: Lists/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null) return NotFound();

            var uid = _userManager.GetUserId(User)!;

            var list = await _context.Lists
                .Include(l => l.ListRecipes)
                    .ThenInclude(lr => lr.Recipe)
                .AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    (l.OwnerId == uid || l.Private == false));

            if (list is null) return NotFound();

            // Show actual owner's email
            ViewBag.OwnerEmail = await _context.Users
                .Where(u => u.Id == list.OwnerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            return View(list);
        }

        // GET: Lists/Create
        public async Task<IActionResult> Create()
        {
            var uid = _userManager.GetUserId(User)!;
            var vm = new ListEditVm
            {
                List = new List { Private = true, ListType = ListType.Recipes },
                RecipeChoices = await BuildRecipeChoicesAsync(uid)
            };
            return View(vm);
        }

        // POST: Lists/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ListEditVm vm)
        {
            var uid = _userManager.GetUserId(User)!;

            vm.List.OwnerId = uid;
            ModelState.Remove("List.OwnerId");

            if (!ModelState.IsValid)
            {
                vm.RecipeChoices = await BuildRecipeChoicesAsync(uid, vm.SelectedRecipeIds);
                return View(vm);
            }

            _context.Lists.Add(vm.List);
            await _context.SaveChangesAsync();

            if (vm.SelectedRecipeIds?.Length > 0)
            {
                var links = vm.SelectedRecipeIds.Distinct()
                    .Select(rid => new ListRecipe { ListId = vm.List.Id, RecipeId = rid });
                _context.ListRecipes.AddRange(links);
                await _context.SaveChangesAsync();
            }

            // Alerts (TempData) + Log
            TempData["Success"] = $"List '{vm.List.Name}' created.";
            _logger.LogInformation("List {ListId} created by user {UserId}.", vm.List.Id, uid);  // ← added

            return RedirectToAction(nameof(Index));
        }

        // GET: Lists/Edit/5 (owner-only)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var uid = _userManager.GetUserId(User)!;

            var list = await _context.Lists
                .Include(l => l.ListRecipes)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list is null) return NotFound();
            if (list.OwnerId != uid) return Forbid();

            var selected = list.ListRecipes.Select(lr => lr.RecipeId).ToArray();

            var vm = new ListEditVm
            {
                List = list,
                SelectedRecipeIds = selected,
                RecipeChoices = await BuildRecipeChoicesAsync(uid, selected)
            };
            return View(vm);
        }

        // POST: Lists/Edit/5 (owner-only)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ListEditVm vm)
        {
            var uid = _userManager.GetUserId(User)!;

            var list = await _context.Lists
                .Include(l => l.ListRecipes)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list is null) return NotFound();
            if (list.OwnerId != uid) return Forbid();

            ModelState.Remove("List.OwnerId");

            if (!ModelState.IsValid)
            {
                vm.List = list;
                vm.RecipeChoices = await BuildRecipeChoicesAsync(uid, vm.SelectedRecipeIds);
                return View(vm);
            }

            list.Name = vm.List.Name;
            list.ListType = vm.List.ListType;
            list.Private = vm.List.Private;

            var selected = new HashSet<int>(vm.SelectedRecipeIds ?? Array.Empty<int>());
            var existing = new HashSet<int>(list.ListRecipes.Select(lr => lr.RecipeId));

            var toAdd = selected.Except(existing)
                .Select(rid => new ListRecipe { ListId = list.Id, RecipeId = rid });
            var toRemove = list.ListRecipes.Where(lr => !selected.Contains(lr.RecipeId)).ToList();

            _context.ListRecipes.RemoveRange(toRemove);
            _context.ListRecipes.AddRange(toAdd);

            await _context.SaveChangesAsync();

            // Alerts (TempData) + Log
            TempData["Success"] = $"List '{list.Name}' updated.";
            _logger.LogInformation("List {ListId} updated by user {UserId}.", list.Id, uid);      // ← added

            return RedirectToAction(nameof(Details), new { id = list.Id });
        }

        // GET: Lists/Delete/5 (owner-only)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();
            var uid = _userManager.GetUserId(User)!;

            var list = await _context.Lists
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list is null) return NotFound();
            if (list.OwnerId != uid) return Forbid();

            ViewBag.OwnerEmail = await _context.Users
                .Where(u => u.Id == list.OwnerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            return View(list);
        }

        // POST: Lists/Delete/5 (owner-only)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var uid = _userManager.GetUserId(User)!;

            var list = await _context.Lists.FirstOrDefaultAsync(l => l.Id == id);
            if (list is null) return NotFound();
            if (list.OwnerId != uid) return Forbid();

            _context.Lists.Remove(list);
            await _context.SaveChangesAsync();

            // Alerts (TempData) + Log
            TempData["Success"] = $"List '{list.Name}' deleted.";
            _logger.LogInformation("List {ListId} deleted by user {UserId}.", list.Id, uid);      // ← added

            return RedirectToAction(nameof(Index));
        }
    }
}
