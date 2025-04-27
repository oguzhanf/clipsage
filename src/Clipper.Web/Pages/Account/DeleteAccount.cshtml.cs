using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Clipper.Web.Pages.Account
{
    [Authorize]
    public class DeleteAccountModel : PageModel
    {
        public string UserEmail { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Please enter your email address to confirm deletion.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string ConfirmEmail { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "You must confirm that you understand this action is permanent.")]
        public bool ConfirmDelete { get; set; }

        [BindProperty]
        public string? DeletionReason { get; set; }

        [BindProperty]
        public string? Feedback { get; set; }

        public void OnGet()
        {
            UserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        }

        public IActionResult OnPost()
        {
            UserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (ConfirmEmail != UserEmail)
            {
                ModelState.AddModelError("ConfirmEmail", "The email address you entered does not match your account email.");
                return Page();
            }

            // In a real application, this would:
            // 1. Cancel any active subscriptions
            // 2. Delete all user data from the database
            // 3. Remove the user from Azure AD B2C
            // 4. Log the user out

            // For now, we'll just redirect to the home page with a message
            TempData["StatusMessage"] = "Your account has been successfully deleted. We're sorry to see you go!";
            return RedirectToPage("/Index");
        }
    }
}
