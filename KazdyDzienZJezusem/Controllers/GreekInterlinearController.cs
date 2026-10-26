using System.Text.Json;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KazdyDzienZJezusem.Controllers;

[ApiController]
[Route("api/greek-interlinear")]
public sealed class GreekInterlinearController(GreekInterlinearService translator) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("greek-interlinear")]
    [RequestSizeLimit(100_000)]
    public async Task<IActionResult> Translate(GreekInterlinearRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await translator.TranslateAsync(request.Text ?? "", request.Context ?? request.Text ?? "", cancellationToken));
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return StatusCode(503, new { error = exception.Message }); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException
            || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StatusCode(503, new { error = "Greek translation is unavailable. Please try again later." });
        }
    }
}

public sealed record GreekInterlinearRequest(string? Text, string? Context);
