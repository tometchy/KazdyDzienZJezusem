using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Mvc;

namespace KazdyDzienZJezusem.Controllers;

[Route("Tagi")]
public sealed class TagsController(TagRepository tagRepository) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View(new TagIndexViewModel(tagRepository.GetAll()));

    [HttpGet("{name}")]
    public IActionResult Details(string name)
    {
        var tag = tagRepository.Find(name);
        return tag is null ? NotFound() : View(new TagDetailsViewModel(tag));
    }
}
