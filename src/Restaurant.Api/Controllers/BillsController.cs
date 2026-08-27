using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.Bills;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.PaymentsCheckout)]
[Route("api/[controller]")]
public sealed class BillsController : ControllerBase
{
    private readonly IBillService _service;
    private readonly ApplicationDbContext _db;
    private readonly IGeneratedFileStorage _fileStorage;

    public BillsController(
        IBillService service,
        ApplicationDbContext db,
        IGeneratedFileStorage fileStorage)
    {
        _service = service;
        _db = db;
        _fileStorage = fileStorage;
    }

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

    [HttpGet("{id:guid}/receipt/pdf")]
    public async Task<IActionResult> DownloadReceiptPdf(Guid id, CancellationToken cancellationToken = default)
    {
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bill?.ReceiptPdfRelativePath is null)
            return NotFound();

        var file = await _fileStorage.OpenReadAsync(bill.ReceiptPdfRelativePath, cancellationToken);
        if (file is null)
            return NotFound();

        return File(file.Stream, "application/pdf", $"factura-{bill.DianConsecutiveNumber}.pdf");
    }

    [HttpGet("{id:guid}/receipt/xml")]
    public async Task<IActionResult> DownloadReceiptXml(Guid id, CancellationToken cancellationToken = default)
    {
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bill?.ReceiptXmlRelativePath is null)
            return NotFound();

        var file = await _fileStorage.OpenReadAsync(bill.ReceiptXmlRelativePath, cancellationToken);
        if (file is null)
            return NotFound();

        return File(file.Stream, "application/xml", $"factura-{bill.DianConsecutiveNumber}.xml");
    }
}
