using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Clipper.Web.Pages.Account
{
    public class AuthErrorModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string Provider { get; set; } = "the authentication provider";

        [BindProperty(SupportsGet = true)]
        public string Error { get; set; } = "Unknown error";

        public string ErrorMessage => string.IsNullOrEmpty(Error) ? "Unknown error" : Error;

        public void OnGet()
        {
            // Log the authentication error
            System.Diagnostics.Debug.WriteLine($"Authentication error from {Provider}: {Error}");
        }
    }
}
