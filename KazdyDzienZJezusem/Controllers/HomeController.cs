using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KazdyDzienZJezusem.Models;

namespace KazdyDzienZJezusem.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Biblia()
    {
        return RedirectToAction("Index", "Bible");
    }

    [HttpGet("/Ojcowie")]
    public IActionResult Ojcowie()
    {
        return View();
    }

    [HttpGet("/Teksty")]
    public IActionResult Teksty()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet("/Error")]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
