using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Clipper.Web.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login(string returnUrl = "/")
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public IActionResult ExternalLogin(string provider, string returnUrl = "/")
        {
            try
            {
                // Log the authentication attempt
                Debug.WriteLine($"External login attempt with provider: {provider}");

                var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
                var properties = new AuthenticationProperties
                {
                    RedirectUri = redirectUrl,
                    // Add items to help with debugging
                    Items =
                    {
                        { "LoginProvider", provider },
                        { "ReturnUrl", returnUrl }
                    }
                };

                return Challenge(properties, provider);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ExternalLogin: {ex.Message}");
                return RedirectToPage("/Account/AuthError", new { provider = provider, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/", string remoteError = null)
        {
            try
            {
                Debug.WriteLine($"ExternalLoginCallback called. RemoteError: {remoteError ?? "none"}");

                if (remoteError != null)
                {
                    Debug.WriteLine($"Remote error from provider: {remoteError}");
                    return RedirectToPage("/Account/AuthError", new { provider = "external", error = remoteError });
                }

                var info = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                Debug.WriteLine($"Authentication result: {(info.Succeeded ? "Success" : "Failed")}");

                if (info.Succeeded)
                {
                    var claims = info.Principal?.Claims;
                    if (claims != null)
                    {
                        foreach (var claim in claims)
                        {
                            Debug.WriteLine($"Claim: {claim.Type} = {claim.Value}");
                        }
                    }

                    return LocalRedirect(returnUrl);
                }
                else
                {
                    Debug.WriteLine("Authentication failed in callback");
                    return RedirectToPage("/Account/AuthError", new { provider = "external", error = "Authentication failed" });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in ExternalLoginCallback: {ex.Message}");
                return RedirectToPage("/Account/AuthError", new { provider = "external", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
