using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KazdyDzienZJezusem.Controllers;

[ApiController]
[Route("api/greek-interlinear")]
public sealed class GreekInterlinearController(GreekInterlinearService translator, GreekMorphologyService morphology) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("greek-interlinear")]
    [RequestSizeLimit(100_000)]
    public async Task<IActionResult> Translate(GreekInterlinearRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await translator.TranslateAsync(request.Text ?? "", request.Context ?? request.Text ?? "", cancellationToken, request.Reference);
            if (!request.Morphology) return Ok(result);
            var analysis = await morphology.AnalyzeAsync(request.Text ?? "", request.Reference, cancellationToken);
            return Ok(new { result.Words, Morphology = analysis.Words });
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return StatusCode(503, new { error = exception.Message }); }
    }
}

public sealed record GreekInterlinearRequest(string? Text, string? Context, string? Reference = null, bool Morphology = false);
