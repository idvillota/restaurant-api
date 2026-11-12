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
    private readonly IDailyClosureService _dailyClosure;
    private readonly IPlanillaPdfService _planillas;

    public CashierShiftsController(
        ICashierShiftService service,
        IDailyClosureService dailyClosure,
        IPlanillaPdfService planillas)
    {
        _service = service;
        _dailyClosure = dailyClosure;
        _planillas = planillas;
    }

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

    [HttpGet("providers")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public Task<ActionResult<IReadOnlyList<CashierProviderOptionDto>>> ListProviders(
        CancellationToken cancellationToken) =>
        OkResult(_service.ListActiveProvidersAsync(cancellationToken));

    [HttpGet("day-summary")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public async Task<ActionResult<DailyClosureReportDto>> GetDaySummary(
        [FromQuery] DateOnly? businessDate,
        CancellationToken cancellationToken)
    {
        var date = businessDate
            ?? (await _service.GetBusinessDayContextAsync(cancellationToken)).BusinessDate;
        return Ok(await _dailyClosure.GetDailyReportAsync(date, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public Task<ActionResult<CashierShiftReportDto>> GetReport(Guid id, CancellationToken cancellationToken) =>
        OkResult(_service.GetShiftReportAsync(id, cancellationToken));

    [HttpGet("{id:guid}/pdf")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public async Task<IActionResult> DownloadClosurePdf(Guid id, CancellationToken cancellationToken)
    {
        var file = await _planillas.OpenShiftPdfAsync(id, cancellationToken);
        if (file is null)
            return NotFound();

        return File(file.Stream, "application/pdf", $"cierre-turno-{id:N}.pdf");
    }

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

    [HttpPost("{id:guid}/force-close")]
    [RequireFeature(FeatureCodes.ReportsDailyClosure)]
    public async Task<ActionResult<CashierShiftReportDto>> ForceClose(
        Guid id,
        [FromBody] ForceCloseCashierShiftDto? dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _service.ForceCloseShiftAsync(id, dto?.Notes, cancellationToken);
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

        var canRecordCashAdjustment = User.HasClaim(PermissionClaims.Type, FeatureCodes.ReportsDailyClosure);

        try
        {
            var movement = await _service.RecordCashMovementAsync(
                dto,
                canRecordCashAdjustment,
                cancellationToken);
            return StatusCode(StatusCodes.Status201Created, movement);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("cash-movements/{id:guid}/void")]
    [RequireFeature(FeatureCodes.CashierShifts)]
    public async Task<ActionResult<CashMovementDto>> VoidMovement(
        Guid id,
        [FromBody] VoidCashMovementDto? dto,
        CancellationToken cancellationToken)
    {
        var canVoidClosedShift = User.HasClaim(PermissionClaims.Type, FeatureCodes.ReportsDailyClosure);

        try
        {
            var movement = await _service.VoidCashMovementAsync(
                id,
                dto ?? new VoidCashMovementDto(),
                canVoidClosedShift,
                cancellationToken);
            return Ok(movement);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private async Task<ActionResult<T>> OkResult<T>(Task<T> task) => Ok(await task);
}
