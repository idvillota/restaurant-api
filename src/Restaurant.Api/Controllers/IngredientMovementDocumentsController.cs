using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.IngredientMovements;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.InventoryIngredientMovements)]
[Route("api/[controller]")]
public sealed class IngredientMovementDocumentsController : ControllerBase
{
    private readonly IIngredientMovementDocumentService _service;

    public IngredientMovementDocumentsController(IIngredientMovementDocumentService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PagedResult<IngredientMovementDocumentListItemDto>>> List(
        [FromQuery] ListQuery query,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IngredientMovementDocumentDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<IngredientMovementDocumentDto>> Create(
        [FromBody] CreateIngredientMovementDocumentDto dto,
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
