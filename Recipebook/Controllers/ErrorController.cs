using Microsoft.AspNetCore.Mvc;

namespace Recipebook.Controllers
{
    public class ErrorController : Controller
    {
        public IActionResult Index()
        {
            Response.StatusCode = 500;
            return View();
        }

        [Route("Error/404")]
        public IActionResult NotFoundPage()
        {
            Response.StatusCode = 404;
            return View("NotFound");
        }
    }
}
