using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog.Ingredients;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class IngredientsController : ControllerBase
{
    private readonly IIngredientService _ingredients;

    public IngredientsController(IIngredientService ingredients) => _ingredients = ingredients;

    [HttpGet]
    public async Task<ActionResult<PagedResult<IngredientDto>>> List(
        [FromQuery] ListQuery query,
        CancellationToken cancellationToken = default) =>
        Ok(await _ingredients.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IngredientDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _ingredients.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<IngredientDto>> Create(
        [FromBody] CreateIngredientDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var created = await _ingredients.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<IngredientDto>> Update(
        Guid id,
        [FromBody] UpdateIngredientDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var updated = await _ingredients.UpdateAsync(id, dto, cancellationToken);
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
        var ok = await _ingredients.SoftDeleteAsync(id, cancellationToken);
        return ok ? NoContent() : NotFound();
    }
}
