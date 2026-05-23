using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.Bills;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class BillsController : ControllerBase
{
    private readonly IBillService _service;

    public BillsController(IBillService service) => _service = service;

    [HttpGet("payable")]
    public async Task<ActionResult<IReadOnlyList<PayableTableGroupDto>>> ListPayable(
        [FromQuery] string? tableSearch,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListPayableByTableSearchAsync(tableSearch, cancellationToken));

    [HttpPost("preview")]
    public async Task<ActionResult<CheckoutTotalsDto>> Preview(
        [FromBody] CheckoutPreviewDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await _service.PreviewCheckoutAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("finalize")]
    public async Task<ActionResult<BillDto>> Finalize(
        [FromBody] FinalizeCheckoutDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await _service.FinalizeCheckoutAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
