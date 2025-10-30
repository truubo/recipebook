using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipebook.Data;
using Recipebook.Models;
using System.Security.Claims;

[Authorize]
public class FavoritesController : Controller
{
    private readonly ApplicationDbContext _db;
    public FavoritesController(ApplicationDbContext db) => _db = db;

    // POST /Favorites/Toggle
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int recipeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var existing = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == recipeId);

        if (existing == null)
            _db.Favorites.Add(new Favorite { UserId = userId, RecipeId = recipeId });
        else
            _db.Favorites.Remove(existing);

        await _db.SaveChangesAsync();

        // go back to the page you were on
        return Redirect(Request.Headers["Referer"].ToString());
    }
}
