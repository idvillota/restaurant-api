using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Platform;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/platform/tenants")]
[RequirePlatformAdmin]
public sealed class PlatformTenantsController : ControllerBase
{
    private readonly ITenantInitialDataImportService _importService;

    public PlatformTenantsController(ITenantInitialDataImportService importService)
    {
        _importService = importService;
    }

    /// <summary>Download Excel template for initial tenant load.</summary>
    [HttpGet("initial-data-template")]
    public IActionResult DownloadTemplate()
    {
        var bytes = _importService.BuildTemplate();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "tenant-initial-data-template.xlsx");
    }

    /// <summary>
    /// Validates and imports initial tenant data from Excel (all-or-nothing).
    /// Sheets: Tenant, Billing, ProductTypes, Products, Ingredients, Recipes, DiningTables.
    /// </summary>
    [HttpPost("load-initial-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TenantInitialDataImportResultDto>> LoadInitialData(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Se requiere un archivo Excel (.xlsx).",
                errors = new[]
                {
                    new TenantInitialDataErrorDto
                    {
                        Sheet = string.Empty,
                        Message = "Archivo vacío o no enviado. Use el campo 'file'.",
                    },
                },
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "El archivo debe ser .xlsx.",
                errors = new[]
                {
                    new TenantInitialDataErrorDto
                    {
                        Sheet = string.Empty,
                        Field = "file",
                        Message = "Formato inválido. Solo se acepta .xlsx.",
                    },
                },
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.ImportAsync(stream, cancellationToken);
            return Created($"/api/platform/tenants/{result.TenantId}", result);
        }
        catch (TenantInitialDataValidationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message,
                errors = ex.Errors,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
