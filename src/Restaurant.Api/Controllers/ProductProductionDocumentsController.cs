using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.ProductProductions;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.InventoryProductProductions)]
[Route("api/[controller]")]
public sealed class ProductProductionDocumentsController : ControllerBase
{
    private readonly IProductProductionDocumentService _service;

    public ProductProductionDocumentsController(IProductProductionDocumentService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductProductionDocumentListItemDto>>> List(
        [FromQuery] ListQuery query,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(query, cancellationToken));

    [HttpGet("next-document-number")]
    public async Task<ActionResult<object>> SuggestNextDocumentNumber(CancellationToken cancellationToken = default) =>
        Ok(new { documentNumber = await _service.SuggestNextDocumentNumberAsync(cancellationToken) });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductProductionDocumentDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ProductProductionDocumentDto>> Create(
        [FromBody] CreateProductProductionDocumentDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var created = await _service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
