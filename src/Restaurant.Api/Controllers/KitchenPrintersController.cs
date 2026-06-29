using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.KitchenPrinters;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.SettingsTenant)]
[Route("api/kitchen/printers")]
public sealed class KitchenPrintersController : ControllerBase
{
    private readonly IKitchenPrinterService _service;

    public KitchenPrintersController(IKitchenPrinterService service) => _service = service;

    [HttpGet("stations")]
    public async Task<ActionResult<IReadOnlyList<PrinterStationDto>>> ListStations(
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListStationsAsync(cancellationToken));

    [HttpPost("stations")]
    public async Task<ActionResult<PrinterStationDto>> CreateStation(
        [FromBody] CreatePrinterStationDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await _service.CreateStationAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("stations/{id:guid}")]
    public async Task<ActionResult<PrinterStationDto>> UpdateStation(
        Guid id,
        [FromBody] UpdatePrinterStationDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var updated = await _service.UpdateStationAsync(id, dto, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("stations/{id:guid}")]
    public async Task<IActionResult> DeleteStation(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _service.SoftDeleteStationAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("routing")]
    public async Task<ActionResult<ProductTypePrinterRoutingDto>> GetRouting(
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetRoutingAsync(cancellationToken));

    [HttpPut("routing")]
    public async Task<ActionResult<ProductTypePrinterRoutingDto>> UpdateRouting(
        [FromBody] UpdateProductTypePrinterRoutingDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await _service.UpdateRoutingAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
