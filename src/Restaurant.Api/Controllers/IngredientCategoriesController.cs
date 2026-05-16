using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog.IngredientCategories;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class IngredientCategoriesController : ControllerBase
{
    private readonly IIngredientCategoryService _service;

    public IngredientCategoriesController(IIngredientCategoryService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IngredientCategoryDto>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IngredientCategoryDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<IngredientCategoryDto>> Create(
        [FromBody] CreateIngredientCategoryDto dto,
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
    public async Task<ActionResult<IngredientCategoryDto>> Update(
        Guid id,
        [FromBody] UpdateIngredientCategoryDto dto,
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
