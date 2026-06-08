using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog;
using Restaurant.Application.Features.Catalog.Products;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.CatalogProducts)]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> List(
        [FromQuery] ListQuery query,
        CancellationToken cancellationToken = default) =>
        Ok(await _products.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductListItemDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _products.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ProductListItemDto>> Create(
        [FromBody] CreateProductDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var created = await _products.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductListItemDto>> Update(
        Guid id,
        [FromBody] UpdateProductDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var updated = await _products.UpdateAsync(id, dto, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken = default)
    {
        var ok = await _products.SoftDeleteAsync(id, cancellationToken);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/recipe")]
    public async Task<ActionResult<ProductRecipeDto>> GetRecipe(Guid id, CancellationToken cancellationToken = default)
    {
        var recipe = await _products.GetRecipeAsync(id, cancellationToken);
        return recipe is null ? NotFound() : Ok(recipe);
    }

    [HttpPut("{id:guid}/image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ProductListItemDto>> SetImage(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        try
        {
            await using var stream = file.OpenReadStream();
            var updated = await _products.SetImageAsync(id, stream, file.FileName, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/image")]
    public async Task<ActionResult<ProductListItemDto>> RemoveImage(Guid id, CancellationToken cancellationToken = default)
    {
        var updated = await _products.RemoveImageAsync(id, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{id:guid}/recipe")]
    public async Task<ActionResult<ProductRecipeDto>> SetRecipe(
        Guid id,
        [FromBody] SetProductRecipeDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var recipe = await _products.SetRecipeAsync(id, dto, cancellationToken);
            return recipe is null ? NotFound() : Ok(recipe);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/bundle")]
    public async Task<ActionResult<ProductBundleDto>> GetBundle(Guid id, CancellationToken cancellationToken = default)
    {
        var bundle = await _products.GetBundleAsync(id, cancellationToken);
        return bundle is null ? NotFound() : Ok(bundle);
    }

    [HttpPut("{id:guid}/bundle")]
    public async Task<ActionResult<ProductBundleDto>> SetBundle(
        Guid id,
        [FromBody] SetProductBundleDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var bundle = await _products.SetBundleAsync(id, dto, cancellationToken);
            return bundle is null ? NotFound() : Ok(bundle);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
