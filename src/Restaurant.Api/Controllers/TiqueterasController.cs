using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.LunchTickets;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/tiqueteras")]
public sealed class TiqueterasController(
    ITiqueteraConfigService config,
    ITiqueteraSalesService sales) : ControllerBase
{
    [HttpGet]
    [RequireTiqueterasAccess]
    public async Task<ActionResult<TiqueteraConfigDto>> GetConfig(CancellationToken cancellationToken = default) =>
        Ok(await config.GetConfigAsync(cancellationToken));

    [HttpPost("packs")]
    [RequireFeature(FeatureCodes.CatalogProducts)]
    public async Task<ActionResult<TiqueteraPackDto>> CreatePack(
        [FromBody] CreateTiqueteraPackDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var created = await config.CreatePackAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetConfig), created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("packs/{productId:guid}")]
    [RequireFeature(FeatureCodes.CatalogProducts)]
    public async Task<ActionResult<TiqueteraPackDto>> UpdatePack(
        Guid productId,
        [FromBody] UpdateTiqueteraPackDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var updated = await config.UpdatePackAsync(productId, dto, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("packs/{productId:guid}")]
    [RequireFeature(FeatureCodes.CatalogProducts)]
    public async Task<IActionResult> DeactivatePack(Guid productId, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await config.DeactivatePackAsync(productId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("redeemable-products")]
    [RequireFeature(FeatureCodes.CatalogProducts)]
    public async Task<ActionResult<TiqueteraConfigDto>> SetRedeemableProducts(
        [FromBody] SetTiqueteraRedeemableProductsDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await config.SetRedeemableProductsAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("sell")]
    [RequireFeature(FeatureCodes.PaymentsCheckout)]
    public async Task<ActionResult<SellTiqueteraPackResultDto>> SellPack(
        [FromBody] SellTiqueteraPackDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            return Ok(await sales.SellPackAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
