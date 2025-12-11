using System.Security.Claims;
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
        private readonly ApplicationDbContext _context;

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

            string userVoteResult;

            if (vote == null)
            {
                _context.RecipeVotes.Add(new RecipeVote { RecipeId = recipeId, UserId = userId, IsLike = true });
                userVoteResult = "like";
            }
            else if (vote.IsLike)
            {
                _context.RecipeVotes.Remove(vote);
                userVoteResult = "";
            }
            else
            {
                vote.IsLike = true;
                _context.RecipeVotes.Update(vote);
                userVoteResult = "like";
            }

            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return await ReturnVoteJson(recipeId, userVoteResult);

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

            string userVoteResult;

            if (vote == null)
            {
                _context.RecipeVotes.Add(new RecipeVote { RecipeId = recipeId, UserId = userId, IsLike = false });
                userVoteResult = "dislike";
            }
            else if (!vote.IsLike)
            {
                _context.RecipeVotes.Remove(vote);
                userVoteResult = "";
            }
            else
            {
                vote.IsLike = false;
                _context.RecipeVotes.Update(vote);
                userVoteResult = "dislike";
            }

            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return await ReturnVoteJson(recipeId, userVoteResult);

            return RedirectToLocal(returnUrl, recipeId);
        }

        private async Task<IActionResult> ReturnVoteJson(int recipeId, string userVote)
        {
            var likes = await _context.RecipeVotes.CountAsync(v => v.RecipeId == recipeId && v.IsLike);
            var dislikes = await _context.RecipeVotes.CountAsync(v => v.RecipeId == recipeId && !v.IsLike);

            return Json(new
            {
                likes,
                dislikes,
                userVote   // "like", "dislike", or ""
            });
        }

    }
}
