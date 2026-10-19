using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrintController : Controller
    {
        private readonly IPrintService _printService;

        public PrintController(IPrintService printService)
        {
            _printService = printService;
        }

        [HttpGet("{type}/{id:guid}")]
        [Authorize] // aplicar política/feature según convenga
        public async Task<IActionResult> DownloadXml(string type, Guid id, [FromQuery] string? path, CancellationToken cancellationToken)
        {
            // 1. validar tipo
            if (!string.Equals(type, "factura", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "comanda", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Tipo inválido. Use 'factura' o 'comanda'.");

            if (!Enum.TryParse<PrintFileType>(type, true, out var fileType))
                return BadRequest("Tipo inválido. Use 'factura' o 'comanda'.");

            var content = await _printService.GetDocumentAsync(id, fileType, path, cancellationToken);
            if (content is null || content.Stream is null)
                return NotFound();

            var fileName = Path.GetFileName(path ?? $"document-{id}.xml") ?? $"{type}-{id}.xml";
            return File(content.Stream, content.ContentType ?? "application/xml", fileName, enableRangeProcessing: false);
        }
    }
}
