using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KazdyDzienZJezusem.Controllers;

[Route("Tag")]
public sealed class TagsController(TagRepository tags) : Controller
{
    [HttpGet("/Tagi")]
    public IActionResult Index() => View(tags.GetAll());

    [HttpGet("{slug}")]
    public IActionResult Details(string slug, [FromServices] TagPassageReader passages)
    {
        var tag = tags.Find(slug);
        return tag is null ? NotFound() : View(passages.Read(tag));
    }

    [HttpGet("/api/tags")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Catalog() => Json(tags.GetAll().Select(tag => new { tag.Slug, tag.Name }));

    [HttpGet("/api/tags/fragment")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Fragment(string fragment, [FromServices] FragmentValidator validator)
    {
        try { return Json(tags.GetTags(validator.Validate(fragment))); }
        catch (TagEditException exception) { return StatusCode(exception.StatusCode, new { error = exception.Message }); }
    }

    [HttpPost("/api/tags/fragment")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("editor")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Add([FromBody] AddTagRequest request,
        [FromServices] EditorSessions sessions, [FromServices] FragmentValidator validator,
        [FromServices] TagEditingService editor, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var token = sessions.GetToken(HttpContext);
        if (token is null) return Unauthorized(new { error = "Your session has expired. Open the login page again." });
        try
        {
            var name = TagFileEditor.ValidateName(request?.Name);
            var fragment = validator.Validate(request?.Fragment);
            var indexed = await editor.AddAsync(name, fragment, token, cancellationToken);
            return Ok(new
            {
                tags = tags.GetTags(fragment),
                warning = indexed ? null : "Saved in the repository. The shared index is recovering automatically."
            });
        }
        catch (TagEditException exception) { return StatusCode(exception.StatusCode, new { error = exception.Message }); }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return StatusCode(503, new { error = "The remote service is unavailable. Please retry; repeated additions are safe." });
        }
    }
}

public sealed record AddTagRequest(string? Name, string? Fragment);
