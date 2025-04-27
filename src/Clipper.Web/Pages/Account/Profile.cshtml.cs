using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Clipper.Web.Pages.Account
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Phone { get; set; } = string.Empty;

        [BindProperty]
        public bool EmailNotifications { get; set; } = true;

        [BindProperty]
        public bool MarketingEmails { get; set; } = true;

        public void OnGet()
        {
            // In a real application, this would fetch the user's profile from a database
            // For now, we'll just use the claims from the authenticated user
            Name = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
            Phone = User.FindFirst(ClaimTypes.MobilePhone)?.Value ?? string.Empty;
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // In a real application, this would update the user's profile in a database
            // For now, we'll just redirect back to the profile page with a success message
            TempData["StatusMessage"] = "Your profile has been updated successfully.";
            return RedirectToPage();
        }
    }
}
