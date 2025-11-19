using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipebook.Data;     
using Recipebook.Models;

namespace Recipebook.Controllers
{
    [Authorize]
    public class VotesController : Controller
    {
        private readonly ApplicationDbContext _context; // or your DbContext name

        public VotesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        private IActionResult RedirectToLocal(string? returnUrl, int recipeId)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Fallback to recipe details
            return RedirectToAction("Details", "Recipes", new { id = recipeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int recipeId, string? returnUrl)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();

            var vote = await _context.RecipeVotes
                .SingleOrDefaultAsync(v => v.RecipeId == recipeId && v.UserId == userId);

            if (vote == null)
            {
                // First time → create like
                _context.RecipeVotes.Add(new RecipeVote
                {
                    RecipeId = recipeId,
                    UserId = userId,
                    IsLike = true
                });
            }
            else if (vote.IsLike)
            {
                // Already liked → remove (unlike)
                _context.RecipeVotes.Remove(vote);
            }
            else
            {
                // Was a dislike → switch to like
                vote.IsLike = true;
                _context.RecipeVotes.Update(vote);
            }

            await _context.SaveChangesAsync();
            return RedirectToLocal(returnUrl, recipeId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDislike(int recipeId, string? returnUrl)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();

            var vote = await _context.RecipeVotes
                .SingleOrDefaultAsync(v => v.RecipeId == recipeId && v.UserId == userId);

            if (vote == null)
            {
                // First time → create dislike
                _context.RecipeVotes.Add(new RecipeVote
                {
                    RecipeId = recipeId,
                    UserId = userId,
                    IsLike = false
                });
            }
            else if (!vote.IsLike)
            {
                // Already disliked → remove (undo)
                _context.RecipeVotes.Remove(vote);
            }
            else
            {
                // Was a like → switch to dislike
                vote.IsLike = false;
                _context.RecipeVotes.Update(vote);
            }

            await _context.SaveChangesAsync();
            return RedirectToLocal(returnUrl, recipeId);
        }
    }
}
