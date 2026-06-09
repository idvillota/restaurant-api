using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.ReportsDailyClosure)]
[Route("api/daily-closures")]
public sealed class DailyClosuresController : ControllerBase
{
    private readonly IDailyClosureService _service;

    public DailyClosuresController(IDailyClosureService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<IReadOnlyList<DailyClosureSummaryDto>>> List(CancellationToken cancellationToken) =>
        OkResult(_service.ListClosuresAsync(cancellationToken));

    [HttpGet("{businessDate}")]
    public Task<ActionResult<DailyClosureReportDto>> GetReport(
        DateOnly businessDate,
        CancellationToken cancellationToken) =>
        OkResult(_service.GetDailyReportAsync(businessDate, cancellationToken));

    [HttpPost("{businessDate}/close")]
    public async Task<ActionResult<DailyClosureReportDto>> Close(
        DateOnly businessDate,
        [FromBody] CloseDailyClosureDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var report = await _service.CloseDailyAsync(businessDate, dto, cancellationToken);
            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private async Task<ActionResult<T>> OkResult<T>(Task<T> task) => Ok(await task);
}
