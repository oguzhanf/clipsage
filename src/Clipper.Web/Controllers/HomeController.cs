using Microsoft.AspNetCore.Mvc;

namespace Clipper.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToPage("/Index");
        }
    }
}
