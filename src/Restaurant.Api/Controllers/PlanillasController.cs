using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.ReportsDailyClosure)]
[Route("api/planillas")]
public sealed class PlanillasController : ControllerBase
{
    private readonly IPlanillaPdfService _planillas;

    public PlanillasController(IPlanillaPdfService planillas) => _planillas = planillas;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanillaListItemDto>>> List(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _planillas.ListDailyPlanillasAsync(from, to, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{businessDate}/pdf")]
    public async Task<IActionResult> Download(
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var file = await _planillas.OpenDailyPlanillaAsync(businessDate, cancellationToken);
        if (file is null)
            return NotFound();

        return File(file.Stream, "application/pdf", $"Planilla-{businessDate:yyyy-MM-dd}.pdf");
    }
}
