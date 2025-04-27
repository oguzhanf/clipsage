using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;

namespace Clipper.Web.Pages.Account
{
    [Authorize]
    public class SubscriptionModel : PageModel
    {
        public string CurrentPlan { get; set; } = "ClipSage Pro";
        public string BillingCycle { get; set; } = "Monthly";
        public DateTime NextBillingDate { get; set; } = DateTime.Now.AddMonths(1);
        public decimal Price { get; set; } = 9.99m;
        
        public string CardType { get; set; } = "Visa";
        public string LastFourDigits { get; set; } = "4242";
        public string CardExpiry { get; set; } = "05/2026";
        
        public List<InvoiceModel> BillingHistory { get; set; } = new List<InvoiceModel>();

        public void OnGet()
        {
            // In a real application, this would fetch the user's subscription data from a database
            // For now, we'll just use some sample data
            
            // Generate some sample billing history
            BillingHistory = new List<InvoiceModel>
            {
                new InvoiceModel
                {
                    Date = DateTime.Now.AddMonths(-1),
                    Description = "ClipSage Pro - Monthly Subscription",
                    Amount = 9.99m,
                    Status = "Paid",
                    InvoiceUrl = "/invoices/sample.pdf"
                },
                new InvoiceModel
                {
                    Date = DateTime.Now.AddMonths(-2),
                    Description = "ClipSage Pro - Monthly Subscription",
                    Amount = 9.99m,
                    Status = "Paid",
                    InvoiceUrl = "/invoices/sample.pdf"
                },
                new InvoiceModel
                {
                    Date = DateTime.Now.AddMonths(-3),
                    Description = "ClipSage Pro - Monthly Subscription",
                    Amount = 9.99m,
                    Status = "Paid",
                    InvoiceUrl = "/invoices/sample.pdf"
                }
            };
        }
        
        public IActionResult OnPostCancelSubscription()
        {
            // In a real application, this would cancel the user's subscription in the payment system
            // For now, we'll just redirect back to the subscription page with a success message
            TempData["StatusMessage"] = "Your subscription has been canceled. You will have access to premium features until the end of your current billing period.";
            return RedirectToPage();
        }
    }
    
    public class InvoiceModel
    {
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string InvoiceUrl { get; set; } = string.Empty;
    }
}
