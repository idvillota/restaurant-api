using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.PaymentsCheckout)]
[Route("api/[controller]")]
public sealed class PrintController : ControllerBase
{
    private readonly IPrintService _printService;

    public PrintController(IPrintService printService)
    {
        _printService = printService;
    }

    [HttpGet("{type}/{id:guid}")]
    public async Task<IActionResult> DownloadXml(string type, Guid id, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PrintFileType>(type, true, out var fileType) ||
            (fileType != PrintFileType.Factura && fileType != PrintFileType.Comanda))
            return BadRequest("Tipo inválido. Use 'factura' o 'comanda'.");

        var content = await _printService.GetDocumentAsync(id, fileType, cancellationToken);
        if (content is null || content.Stream is null)
            return NotFound();

        var fileName = $"{type}-{id}.xml";
        return File(content.Stream, content.ContentType ?? "application/xml", fileName, enableRangeProcessing: false);
    }
}
