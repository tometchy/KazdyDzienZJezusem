using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Mvc;

namespace KazdyDzienZJezusem.Controllers;

[Route("Tagi")]
public sealed class TagsController(TagRepository tags) : Controller
{
    [HttpGet("{name}")]
    public IActionResult Details(string name)
    {
        var tag = tags.Find(name);
        return tag is null ? NotFound() : View(new TagDetailsViewModel(tag));
    }
}
