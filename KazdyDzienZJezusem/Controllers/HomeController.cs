using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KazdyDzienZJezusem.Models;

namespace KazdyDzienZJezusem.Controllers;

public class HomeController : Controller
{
    public IActionResult Biblia()
    {
        return View();
    }

    public IActionResult Ojcowie()
    {
        return View();
    }

    public IActionResult Teksty()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
