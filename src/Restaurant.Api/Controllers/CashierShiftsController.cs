using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/cashier-shifts")]
public sealed class CashierShiftsController : ControllerBase
{
    private readonly ICashierShiftService _service;

    public CashierShiftsController(ICashierShiftService service) => _service = service;

    [HttpGet("context")]
    [RequireOperationalCashierContextRead]
    public Task<ActionResult<BusinessDayContextDto>> GetContext(CancellationToken cancellationToken) =>
        OkResult(_service.GetBusinessDayContextAsync(cancellationToken));

    [HttpGet("my-open")]
    [RequireOperationalCashierContextRead]
    public async Task<ActionResult<CashierShiftSummaryDto>> GetMyOpen(CancellationToken cancellationToken)
    {
        var shift = await _service.GetMyOpenShiftAsync(cancellationToken);
        return shift is null ? NotFound() : Ok(shift);
    }

    [HttpGet]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public Task<ActionResult<IReadOnlyList<CashierShiftSummaryDto>>> List(
        [FromQuery] DateOnly? businessDate,
        CancellationToken cancellationToken) =>
        OkResult(_service.ListShiftsAsync(businessDate, cancellationToken));

    [HttpGet("{id:guid}")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public Task<ActionResult<CashierShiftReportDto>> GetReport(Guid id, CancellationToken cancellationToken) =>
        OkResult(_service.GetShiftReportAsync(id, cancellationToken));

    [HttpPost("open")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public async Task<ActionResult<CashierShiftSummaryDto>> Open(
        [FromBody] OpenCashierShiftDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var shift = await _service.OpenShiftAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetReport), new { id = shift.Id }, shift);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/close")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public async Task<ActionResult<CashierShiftReportDto>> Close(
        Guid id,
        [FromBody] CloseCashierShiftDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var report = await _service.CloseShiftAsync(id, dto, cancellationToken);
            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("cash-movements")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public async Task<ActionResult<CashMovementDto>> RecordMovement(
        [FromBody] CreateCashMovementDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var movement = await _service.RecordCashMovementAsync(dto, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, movement);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private async Task<ActionResult<T>> OkResult<T>(Task<T> task) => Ok(await task);
}
