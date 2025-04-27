using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Authorization;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add authentication with external providers
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
})
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.CallbackPath = "/signin-google";

    // Add event handlers for debugging authentication issues
    options.Events.OnTicketReceived = context =>
    {
        Debug.WriteLine("Google authentication successful");
        return Task.CompletedTask;
    };

    options.Events.OnRemoteFailure = context =>
    {
        Debug.WriteLine($"Google authentication failed: {context.Failure?.Message}");
        context.Response.Redirect("/Account/AuthError?provider=Google&error=" +
            Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error"));
        context.HandleResponse();
        return Task.CompletedTask;
    };
})
.AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, options =>
{
    options.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
})
.AddFacebook(FacebookDefaults.AuthenticationScheme, options =>
{
    options.AppId = builder.Configuration["Authentication:Facebook:AppId"];
    options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
});

// Add services to the container.
builder.Services.AddRazorPages();

// Add controllers for account management
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/html";

            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;

            await context.Response.WriteAsync($@"
                <html>
                <head>
                    <title>Error - ClipSage</title>
                    <link rel='stylesheet' href='/lib/bootstrap/dist/css/bootstrap.min.css' />
                </head>
                <body>
                    <div class='container mt-5'>
                        <div class='row'>
                            <div class='col-md-8 offset-md-2'>
                                <div class='card'>
                                    <div class='card-header bg-danger text-white'>
                                        <h4>Application Error</h4>
                                    </div>
                                    <div class='card-body'>
                                        <p>We're sorry, but something went wrong with the application.</p>
                                        <p>Please try again later or contact support if the problem persists.</p>
                                        <a href='/' class='btn btn-primary'>Return to Home Page</a>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </body>
                </html>");

            // Log the exception
            Debug.WriteLine($"Application error: {exception?.Message}");
        });
    });

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
