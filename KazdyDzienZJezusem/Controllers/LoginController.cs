using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KazdyDzienZJezusem.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LoginController(EditorSessions sessions, IAntiforgery antiforgery) : Controller
{
    [HttpGet("/login")]
    public IActionResult Index() => View(sessions.GetToken(HttpContext) is not null);

    [HttpPost("/login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("editor")]
    [RequestSizeLimit(8192)]
    public IActionResult Login(string? login)
    {
        // UI presence is deliberately independent of GitHub authorization.
        if (string.IsNullOrWhiteSpace(login) || login.Length > 512 || login.Any(char.IsControl))
        {
            ViewData["Error"] = "Enter a login (up to 512 characters).";
            return View("Index", sessions.GetToken(HttpContext) is not null);
        }
        try { sessions.SignIn(HttpContext, login.Trim()); }
        catch (TagEditException exception)
        {
            Response.StatusCode = exception.StatusCode;
            ViewData["Error"] = exception.Message;
            return View("Index", false);
        }
        return Redirect("/login");
    }

    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        try { sessions.SignOut(HttpContext); }
        catch (TagEditException exception)
        {
            Response.StatusCode = exception.StatusCode;
            ViewData["Error"] = exception.Message;
            return View("Index", true);
        }
        return Redirect("/login");
    }

    [HttpGet("/api/editor/session")]
    public IActionResult Session() => Json(new
    {
        loggedIn = sessions.GetToken(HttpContext) is not null,
        csrfToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken
    });
}
