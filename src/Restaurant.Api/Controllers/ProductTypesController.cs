using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog.ProductTypes;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductTypesController : ControllerBase
{
    private readonly IProductTypeService _service;

    public ProductTypesController(IProductTypeService service) => _service = service;

    [HttpGet]
    [RequireSalonCatalogProductTypesRead]
    public async Task<ActionResult<PagedResult<ProductTypeDto>>> List(
        [FromQuery] ListQuery query,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [RequireSalonCatalogProductTypesRead]
    public async Task<ActionResult<ProductTypeDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequireFeature(FeatureCodes.CatalogProductTypes)]
    public async Task<ActionResult<ProductTypeDto>> Create(
        [FromBody] CreateProductTypeDto dto,
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

    [HttpPut("{id:guid}")]
    [RequireFeature(FeatureCodes.CatalogProductTypes)]
    public async Task<ActionResult<ProductTypeDto>> Update(
        Guid id,
        [FromBody] UpdateProductTypeDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var updated = await _service.UpdateAsync(id, dto, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequireFeature(FeatureCodes.CatalogProductTypes)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _service.SoftDeleteAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
