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
using Microsoft.Extensions.Logging;
using Recipebook.Data;
using Recipebook.Models;
using Recipebook.Models.ViewModels;

namespace Recipebook.Controllers
{
    // [Authorize] makes sure only authenticated users can access actions here.
    [Authorize]
    public class ListsController : Controller
    {
        // DI: DbContext for EF Core, UserManager for identity info, ILogger for logs
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ListsController> _logger;

        public ListsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<ListsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // ----------------------- LOGGING SCOPE UTILITIES -------------------------
        // Reason: ILogger.BeginScope returns IDisposable?; some providers may return null.
        // NullScope gives us a harmless, disposable fallback to satisfy the compiler.
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }

        // Wrap logs for a request with additional context properties (UserId, Email, Route)
        // so every log line inside the using(...) automatically includes these.
        private IDisposable BeginUserScope(string userId, string? email = null, string? route = null)
        {
            var state = new Dictionary<string, object?>
            {
                ["UserId"] = userId,
                ["UserEmail"] = email,
                ["Controller"] = nameof(ListsController),
                ["Route"] = route
            };
            return _logger.BeginScope(state) ?? NullScope.Instance;
        }

        // --------------------------- SMALL FORMAT HELPERS ------------------------
        // Pretty print a list of recipe titles for logs.
        private static string JoinTitles(IEnumerable<string> titles)
            => "[" + string.Join(", ", titles) + "]";

        // Show a preview of IDs in logs if needed (currently unused but handy).
        private static IReadOnlyList<int> PreviewIds(IEnumerable<int> ids, int take = 10)
            => ids.Take(take).ToList();

        // ---------------------- RECIPE DROPDOWN (SELECT LIST) --------------------
        // Build choices for multi-select in Create/Edit views.
        // Visibility rule: include recipes authored by current user OR public ones.
        private async Task<List<SelectListItem>> BuildRecipeChoicesAsync(string currentUserId, int[]? preselect = null)
        {
            var recipes = await _context.Recipe
                .AsNoTracking() // faster reads; we won't modify these entities here
                .Where(r => !r.IsArchived)
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

        // --------------------------------- INDEX ---------------------------------
        // GET: Lists
        // Shows two buckets for the signed-in user:
        //   • MyLists: lists you own
        //   • AllLists: lists you own OR any lists marked public (Private == false)
        // Adds optional search by list Name via ?searchString=...
        public async Task<IActionResult> Index(string? searchString, string? scope)
        {
            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users
                .Where(u => u.Id == uid)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Index");

            IQueryable<Recipebook.Models.List> listQ;

            // 🧩 ALL SCOPES NOW INCLUDE INGREDIENT RECIPES CHAIN
            if (scope == "mine")
            {
                listQ = _context.Lists
                    .Where(l => !l.IsArchived && l.OwnerId == uid)
                    .Include(l => l.ListRecipes)
                        .ThenInclude(lr => lr.Recipe)
                            .ThenInclude(r => r.IngredientRecipes)
                                .ThenInclude(ir => ir.Ingredient)
                    .Include(l => l.ListIngredients)
                        .ThenInclude(li => li.Ingredient)
                    .AsNoTracking();
            }
            else if (scope == "ingredients")
            {
                listQ = _context.Lists
                    .Where(l => !l.IsArchived && l.ListType == ListType.Ingredients)
                    .Include(l => l.ListRecipes)
                        .ThenInclude(lr => lr.Recipe)
                            .ThenInclude(r => r.IngredientRecipes)
                                .ThenInclude(ir => ir.Ingredient)
                    .Include(l => l.ListIngredients)
                        .ThenInclude(li => li.Ingredient)
                    .AsNoTracking();
            }
            else // default: all lists
            {
                listQ = _context.Lists
                    .Where(l => !l.IsArchived && (l.OwnerId == uid || !l.Private))
                    .Include(l => l.ListRecipes)
                        .ThenInclude(lr => lr.Recipe)
                            .ThenInclude(r => r.IngredientRecipes)
                                .ThenInclude(ir => ir.Ingredient)
                    .Include(l => l.ListIngredients)
                        .ThenInclude(li => li.Ingredient)
                    .AsNoTracking();
            }

            if (!string.IsNullOrWhiteSpace(searchString))
                listQ = listQ.Where(l => l.Name.Contains(searchString) && !l.IsArchived);

            var lists = await listQ.OrderBy(l => l.Name).ToListAsync();

            // Build owner email map
            var ownerIds = lists.Select(l => l.OwnerId).Distinct().ToList();
            var ownerEmails = await _context.Users
                .Where(u => ownerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email);

            ViewBag.OwnerEmails = ownerEmails;
            ViewBag.SearchString = searchString;
            ViewBag.Scope = scope;

            var vm = new ListsIndexVm
            {
                Lists = lists,
                MyEmail = myEmail,
                MyUserId = uid
            };

            return View(vm);
        }




        // -------------------------------- DETAILS --------------------------------
        // GET: Lists/Details/5
        // Visibility: allow if the list belongs to the user OR the list is public.
        public async Task<IActionResult> Details(int? id, int? sortType)
        {
            if (id is null) return NotFound();

            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users
                .Where(u => u.Id == uid)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Details");

            // ✅ Eager-load full chain so Ingredients view has data
            var list = await _context.Lists
                .Where(l => !l.IsArchived)
                .Include(l => l.ListRecipes)!
                    .ThenInclude(lr => lr.Recipe)!
                        .ThenInclude(r => r.IngredientRecipes)!
                            .ThenInclude(ir => ir.Ingredient)
                .AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    (l.OwnerId == uid || l.Private == false));

            if (list is null)
            {
                _logger.LogInformation("{Email} navigated to /Views/Lists/Details/{ListId}, not found or not visible", myEmail, id);
                return NotFound();
            }

            // Normalize sortType default
            sortType ??= 0;

            // Optional sorting for non-ingredients lists (null-safe and skip archived recipes)
            switch ((SortType)sortType)
            {
                case SortType.AlphabeticalAsc:
                    list.ListRecipes = (list.ListRecipes ?? Enumerable.Empty<ListRecipe>())
                        .Where(lr => lr?.Recipe != null && !lr.Recipe.IsArchived)
                        .OrderBy(lr => lr.Recipe.Title)
                        .ToList();
                    break;

                case SortType.AlphabeticalDesc:
                    list.ListRecipes = (list.ListRecipes ?? Enumerable.Empty<ListRecipe>())
                        .Where(lr => lr?.Recipe != null && !lr.Recipe.IsArchived)
                        .OrderByDescending(lr => lr.Recipe.Title)
                        .ToList();
                    break;
            }

            // Look up owner email for display.
            var ownerEmail = await _context.Users
                .Where(u => u.Id == list.OwnerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            ViewBag.OwnerEmail = ownerEmail;
            ViewBag.SortType = sortType;

            // Build a readable list of recipe titles for the log line.
            var titles = (list.ListRecipes ?? new List<ListRecipe>())
                .Where(lr => lr?.Recipe != null && !lr.Recipe.IsArchived)
                .Select(lr => lr.Recipe!.Title)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            _logger.LogInformation(
                "{Email} navigated to /Views/Lists/Details/{ListId}, name '{Name}', owner {OwnerEmail}, recipes {RecipeCount}, titles {Titles}",
                myEmail, list.Id, list.Name, ownerEmail, list.ListRecipes?.Count ?? 0, JoinTitles(titles));

            return View(list);
        }

        // GET: /Lists/PrintFriendly/5
        // A printer-friendly version of details.
        public async Task<IActionResult> PrintFriendly(int? id, int? sortType)
        {
            if (id is null) return NotFound();

            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users
                .Where(u => u.Id == uid)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Details");

            // ✅ Eager-load full chain so Ingredients view has data
            var list = await _context.Lists
                .Where(l => !l.IsArchived)
                .Include(l => l.ListRecipes)!
                    .ThenInclude(lr => lr.Recipe)!
                        .ThenInclude(r => r.IngredientRecipes)!
                            .ThenInclude(ir => ir.Ingredient)
                .AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    (l.OwnerId == uid || l.Private == false));

            if (list is null)
            {
                _logger.LogInformation("{Email} navigated to /Views/Lists/Details/{ListId}, not found or not visible", myEmail, id);
                return NotFound();
            }

            // Normalize sortType default
            sortType ??= 0;

            // Optional sorting for non-ingredients lists (null-safe and skip archived recipes)
            switch ((SortType)sortType)
            {
                case SortType.AlphabeticalAsc:
                    list.ListRecipes = (list.ListRecipes ?? Enumerable.Empty<ListRecipe>())
                        .Where(lr => lr?.Recipe != null && !lr.Recipe.IsArchived)
                        .OrderBy(lr => lr.Recipe.Title)
                        .ToList();
                    break;

                case SortType.AlphabeticalDesc:
                    list.ListRecipes = (list.ListRecipes ?? Enumerable.Empty<ListRecipe>())
                        .Where(lr => lr?.Recipe != null && !lr.Recipe.IsArchived)
                        .OrderByDescending(lr => lr.Recipe.Title)
                        .ToList();
                    break;
            }

            // Look up owner email for display.
            var ownerEmail = await _context.Users
                .Where(u => u.Id == list.OwnerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            ViewBag.OwnerEmail = ownerEmail;
            ViewBag.SortType = sortType;

            // Build a readable list of recipe titles for the log line.
            var titles = (list.ListRecipes ?? new List<ListRecipe>())
                .Where(lr => lr?.Recipe != null && !lr.Recipe.IsArchived)
                .Select(lr => lr.Recipe!.Title)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            _logger.LogInformation(
                "{Email} navigated to /Views/Lists/Details/{ListId}, name '{Name}', owner {OwnerEmail}, recipes {RecipeCount}, titles {Titles}",
                myEmail, list.Id, list.Name, ownerEmail, list.ListRecipes?.Count ?? 0, JoinTitles(titles));

            return View(list);
        }


        // -------------------------------- CREATE ---------------------------------
        // GET: Lists/Create
        // Initializes a default List object (Private = true, ListType = Recipes) and
        // preloads recipe choices.
        public async Task<IActionResult> Create()
        {
            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Create(GET)");

            _logger.LogInformation("{Email} navigated to /Views/Lists/Create", myEmail);

            var vm = new ListEditVm
            {
                List = new List { Private = true, ListType = ListType.Recipes },
                RecipeChoices = await BuildRecipeChoicesAsync(uid)
            };
            return View(vm);
        }

        // POST: Lists/Create
        // Validates input, stamps OwnerId, creates list, then optionally creates
        // ListRecipe links for any selected recipes. Uses PRG by redirecting.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ListEditVm vm)
        {
            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Create(POST)");

            // Defensive checks to avoid null reference issues if the form posts nothing.
            if (vm is null || vm.List is null)
            {
                ModelState.AddModelError(string.Empty, "Invalid form submission.");
                vm ??= new ListEditVm();
                vm.List ??= new List { Private = true, ListType = ListType.Recipes };
                vm.RecipeChoices = await BuildRecipeChoicesAsync(uid, vm.SelectedRecipeIds);
                _logger.LogInformation("{Email} submitted /Views/Lists/Create, invalid form submission", myEmail);
                return View(vm);
            }

            // Stamp the owner; remove from ModelState to avoid validation conflicts.
            vm.List.OwnerId = uid;
            ModelState.Remove("List.OwnerId");

            if (!ModelState.IsValid)
            {
                _logger.LogInformation("{Email} submitted /Views/Lists/Create, validation failed with {Errors} errors", myEmail, ModelState.ErrorCount);
                vm.RecipeChoices = await BuildRecipeChoicesAsync(uid, vm.SelectedRecipeIds);
                return View(vm);
            }

            // Create the list first (need its Id before linking recipes).
            _context.Lists.Add(vm.List);
            await _context.SaveChangesAsync();

            int linkedCount = 0;
            string linkedTitlesText = "[]";

            // If the user selected recipe IDs, create the join rows.
            if (vm.SelectedRecipeIds is { Length: > 0 })
            {
                var links = vm.SelectedRecipeIds
                    .Distinct()
                    .Select(rid => new ListRecipe { ListId = vm.List.Id, RecipeId = rid })
                    .ToList();

                _context.ListRecipes.AddRange(links);
                await _context.SaveChangesAsync();

                linkedCount = links.Count;

                // Optional: pull recipe titles only for logging readability.
                var addedIds = links.Select(l => l.RecipeId).ToArray();
                var addedTitles = await _context.Recipe
                    .Where(r => addedIds.Contains(r.Id) && !r.IsArchived)
                    .Select(r => r.Title)
                    .ToListAsync();

                linkedTitlesText = JoinTitles(addedTitles);
            }

            _logger.LogInformation(
                "{Email} created list '{Name}' (Id {ListId}), linked {LinkedCount} recipes {Titles}",
                myEmail, vm.List.Name, vm.List.Id, linkedCount, linkedTitlesText);

            // TempData => Bootstrap alert via _Alerts partial (auto-dismiss in layout)
            TempData["Success"] = $"List '{vm.List.Name}' created.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------------------------- EDIT ---------------------------------
        // GET: Lists/Edit/5 (owner-only)
        // Loads the list and the current selection of recipe links for pre-checked UI.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Edit(GET)");

            var list = await _context.Lists
                .Where(l => !l.IsArchived)
                .Include(l => l.ListRecipes)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list is null)
            {
                _logger.LogInformation("{Email} navigated to /Views/Lists/Edit/{ListId}, not found", myEmail, id);
                return NotFound();
            }
            if (list.OwnerId != uid)
            {
                _logger.LogInformation("{Email} navigated to /Views/Lists/Edit/{ListId}, forbidden", myEmail, id);
                return Forbid(); // 403 when someone else tries to edit
            }

            list.ListRecipes ??= new List<ListRecipe>();
            var selected = list.ListRecipes.Select(lr => lr.RecipeId).ToArray();

            var vm = new ListEditVm
            {
                List = list,
                SelectedRecipeIds = selected,
                RecipeChoices = await BuildRecipeChoicesAsync(uid, selected)
            };

            _logger.LogInformation("{Email} navigated to /Views/Lists/Edit/{ListId}, name '{Name}', selected {Selected}",
                myEmail, list.Id, list.Name, selected.Length);

            return View(vm);
        }

        // POST: Lists/Edit/5 (owner-only)
        // Validates changes, reconciles join table (adds/removes ListRecipes), and saves.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ListEditVm vm)
        {
            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Edit(POST)");

            var list = await _context.Lists
                .Where(l => !l.IsArchived)
                .Include(l => l.ListRecipes)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list is null)
            {
                _logger.LogInformation("{Email} submitted /Views/Lists/Edit/{ListId}, not found", myEmail, id);
                return NotFound();
            }
            if (list.OwnerId != uid)
            {
                _logger.LogInformation("{Email} submitted /Views/Lists/Edit/{ListId}, forbidden", myEmail, id);
                return Forbid();
            }

            list.ListRecipes ??= new List<ListRecipe>();
            ModelState.Remove("List.OwnerId"); // OwnerId is server-driven

            if (!ModelState.IsValid)
            {
                _logger.LogInformation("{Email} submitted /Views/Lists/Edit/{ListId}, validation failed with {Errors} errors", myEmail, id, ModelState.ErrorCount);
                vm.List = list; // return existing list back to view
                vm.RecipeChoices = await BuildRecipeChoicesAsync(uid, vm.SelectedRecipeIds);
                return View(vm);
            }

            // Update scalar fields
            list.Name = vm.List.Name;
            list.ListType = vm.List.ListType;
            list.Private = vm.List.Private;

            // Reconcile join rows between currently selected and existing
            var selected = new HashSet<int>(vm.SelectedRecipeIds ?? Array.Empty<int>());
            var existing = new HashSet<int>(list.ListRecipes.Select(lr => lr.RecipeId));

            var toAdd = selected.Except(existing)
                .Select(rid => new ListRecipe { ListId = list.Id, RecipeId = rid })
                .ToList();

            var toRemove = list.ListRecipes.Where(lr => !selected.Contains(lr.RecipeId) && !lr.Recipe.IsArchived).ToList();

            _context.ListRecipes.RemoveRange(toRemove);
            _context.ListRecipes.AddRange(toAdd);
            await _context.SaveChangesAsync();

            // Only for logging readability: pull the titles of changed recipes
            var addedIds = toAdd.Select(a => a.RecipeId).ToArray();
            var removedIds = toRemove.Select(r => r.RecipeId).ToArray();

            var addedTitles = addedIds.Length == 0 ? new List<string>() :
                await _context.Recipe.Where(r => addedIds.Contains(r.Id) && !r.IsArchived).Select(r => r.Title).ToListAsync();
            var removedTitles = removedIds.Length == 0 ? new List<string>() :
                await _context.Recipe.Where(r => removedIds.Contains(r.Id) && !r.IsArchived).Select(r => r.Title).ToListAsync();

            _logger.LogInformation(
                "{Email} updated list '{Name}' (Id {ListId}), added {Added} recipes {AddedTitles}, removed {Removed} recipes {RemovedTitles}",
                myEmail, list.Name, list.Id,
                toAdd.Count, JoinTitles(addedTitles),
                toRemove.Count, JoinTitles(removedTitles));

            TempData["Success"] = $"List '{list.Name}' updated.";
            return RedirectToAction(nameof(Details), new { id = list.Id });
        }

        // --------------------------------- DELETE --------------------------------
        // GET: Lists/Delete/5 (owner-only)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();
            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Delete(GET)");

            var list = await _context.Lists
                .AsNoTracking()
                .Where(l => !l.IsArchived)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list is null)
            {
                _logger.LogInformation("{Email} navigated to /Views/Lists/Delete/{ListId}, not found", myEmail, id);
                return NotFound();
            }
            if (list.OwnerId != uid)
            {
                _logger.LogInformation("{Email} navigated to /Views/Lists/Delete/{ListId}, forbidden", myEmail, id);
                return Forbid();
            }

            var ownerEmail = await _context.Users
                .Where(u => u.Id == list.OwnerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            ViewBag.OwnerEmail = ownerEmail;

            _logger.LogInformation("{Email} navigated to /Views/Lists/Delete/{ListId}, name '{Name}', owner {OwnerEmail}",
                myEmail, list.Id, list.Name, ownerEmail);

            return View(list);
        }

        // POST: Lists/Delete/5 (owner-only)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users
                .Where(u => u.Id == uid)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/Delete(POST)");

            // Include join rows so we can remove them
            var list = await _context.Lists
                .Include(l => l.ListRecipes)
                .Where(l => !l.IsArchived)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list is null)
            {
                _logger.LogInformation("{Email} submitted /Views/Lists/Delete/{ListId}, not found", myEmail, id);
                return NotFound();
            }

            if (list.OwnerId != uid)
            {
                _logger.LogInformation("{Email} submitted /Views/Lists/Delete/{ListId}, forbidden", myEmail, id);
                return Forbid();
            }

            // Soft delete the list
            list.IsArchived = true;
            _context.Update(list);

            // Remove all associated join rows
            if (list.ListRecipes != null && list.ListRecipes.Any())
            {
                _context.ListRecipes.RemoveRange(list.ListRecipes);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("{Email} archived list '{Name}' (Id {ListId}) and removed {Count} join rows",
                myEmail, list.Name, list.Id, list.ListRecipes?.Count ?? 0);

            TempData["Success"] = $"List '{list.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        // -------------------- ADD: QUICK ACTION FROM RECIPE PAGE ------------------
        // POST: Lists/AddToList
        // Minimal endpoint to support "Add to List" button on Recipe pages.
        // Owner-only, prevents duplicates, and redirects back to the recipe (not index).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToList(int listId, int recipeId, string? returnUrl = null)
        {
            var uid = _userManager.GetUserId(User)!;
            var myEmail = await _context.Users
                .Where(u => u.Id == uid)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            using var _ = BeginUserScope(uid, myEmail, "Lists/AddToList(POST)");

            // Load target list and enforce owner-only edits
            var list = await _context.Lists.FirstOrDefaultAsync(l => l.Id == listId);
            if (list is null)
            {
                _logger.LogInformation("{Email} tried AddToList: list {ListId} not found", myEmail, listId);
                TempData["Error"] = "That list wasn’t found.";
                return SafeRedirect(returnUrl);
            }
            if (list.OwnerId != uid)
            {
                _logger.LogInformation("{Email} tried AddToList: list {ListId} forbidden (owner {OwnerId})", myEmail, listId, list.OwnerId);
                TempData["Error"] = "You don’t have permission to modify that list.";
                return SafeRedirect(returnUrl);
            }

            // Ensure the recipe exists (prevents FK violations)
            var recipeExists = await _context.Recipe.AnyAsync(r => r.Id == recipeId);
            if (!recipeExists)
            {
                _logger.LogInformation("{Email} tried AddToList: recipe {RecipeId} not found", myEmail, recipeId);
                TempData["Error"] = "That recipe wasn’t found.";
                return SafeRedirect(returnUrl);
            }

            // Avoid duplicates
            var alreadyLinked = await _context.ListRecipes
                .AnyAsync(lr => lr.ListId == listId && lr.RecipeId == recipeId);

            if (!alreadyLinked)
            {
                _context.ListRecipes.Add(new ListRecipe { ListId = listId, RecipeId = recipeId });
                await _context.SaveChangesAsync();

                _logger.LogInformation("{Email} added recipe {RecipeId} to list '{ListName}' (Id {ListId})",
                    myEmail, recipeId, list.Name, list.Id);

                TempData["Success"] = $"Added to list '{list.Name}'.";
            }
            else
            {
                _logger.LogInformation("{Email} AddToList skipped: recipe {RecipeId} already in list '{ListName}' (Id {ListId})",
                    myEmail, recipeId, list.Name, list.Id);

                TempData["Info"] = $"Already in list '{list.Name}'.";
            }

            // ✅ NEW LOGIC:
            // Respect returnUrl so we stay on the same Recipe Details page.
            // Fall back safely if returnUrl is missing or not local.
            return SafeRedirect(returnUrl);
        }

        // Helper method to prevent open redirects
        private IActionResult SafeRedirect(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // fallback if no returnUrl given
            return RedirectToAction("Index", "Recipes");
        }


    }
}
