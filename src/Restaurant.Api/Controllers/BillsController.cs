using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.Sales.Bills;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class BillsController : ControllerBase
{
    private readonly IBillService _service;
    private readonly ApplicationDbContext _db;
    private readonly string _receiptRoot;

    public BillsController(
        IBillService service,
        ApplicationDbContext db,
        IOptions<SalesReceiptOptions> receiptOptions,
        IHostEnvironment environment)
    {
        _service = service;
        _db = db;
        var rootPath = receiptOptions.Value.RootPath.Trim().TrimEnd('/', '\\');
        _receiptRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.Combine(environment.ContentRootPath, rootPath);
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

        var path = Path.Combine(_receiptRoot, bill.ReceiptPdfRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(path))
            return NotFound();

        return PhysicalFile(
            path,
            "application/pdf",
            $"factura-{bill.DianConsecutiveNumber}.pdf");
    }

    [HttpGet("{id:guid}/receipt/xml")]
    public async Task<IActionResult> DownloadReceiptXml(Guid id, CancellationToken cancellationToken = default)
    {
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bill?.ReceiptXmlRelativePath is null)
            return NotFound();

        var path = Path.Combine(_receiptRoot, bill.ReceiptXmlRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(path))
            return NotFound();

        return PhysicalFile(
            path,
            "application/xml",
            $"factura-{bill.DianConsecutiveNumber}.xml");
    }
}
