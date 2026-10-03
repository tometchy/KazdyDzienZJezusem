using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Mvc;

namespace KazdyDzienZJezusem.Controllers;

[Route("Tag")]
public sealed class TagsController(TagRepository tags) : Controller
{
    [HttpGet("{slug}")]
    public IActionResult Details(string slug)
    {
        var tag = tags.Find(slug);
        return tag is null ? NotFound() : View(new TagDetailsViewModel(tag));
    }
}
