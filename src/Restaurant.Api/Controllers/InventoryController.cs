using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Inventory;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryAvailabilityService _inventory;

    public InventoryController(IInventoryAvailabilityService inventory) => _inventory = inventory;

    [HttpPost("check-availability")]
    public async Task<ActionResult<StockAvailabilityResultDto>> CheckAvailability(
        [FromBody] StockAvailabilityCheckDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await _inventory.CheckLinesAsync(dto, cancellationToken));
    }
}
