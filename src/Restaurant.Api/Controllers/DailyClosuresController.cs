using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;
using Restaurant.Infrastructure.Authorization;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
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
    [Authorize(Roles = $"{SystemRoles.Administrator},{SystemRoles.Owner},{SystemRoles.Manager}")]
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
