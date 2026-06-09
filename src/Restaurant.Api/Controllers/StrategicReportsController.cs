using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Reports;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.ReportsStrategicAi)]
[Route("api/strategic-reports")]
public sealed class StrategicReportsController : ControllerBase
{
    private readonly IStrategicAiReportService _service;

    public StrategicReportsController(IStrategicAiReportService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(StrategicReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StrategicReportDto>> Get(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _service.GetStrategicReportAsync(startDate, endDate, refresh, cancellationToken);
            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
