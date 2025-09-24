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

        public IActionResult NotFound()
        {
            Response.StatusCode = 404;
            return View();
        }
    }
}
