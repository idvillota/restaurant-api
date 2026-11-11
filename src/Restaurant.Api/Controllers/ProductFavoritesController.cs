using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.ProductFavorites;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/user/product-favorites")]
public sealed class ProductFavoritesController : ControllerBase
{
    private readonly IProductFavoriteService _service;

    public ProductFavoritesController(IProductFavoriteService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ProductFavoriteListDto>> List(CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(cancellationToken));

    [HttpPut("{productId:guid}")]
    public async Task<ActionResult<ProductFavoriteListDto>> Add(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.AddAsync(productId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{productId:guid}")]
    public async Task<ActionResult<ProductFavoriteListDto>> Remove(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.RemoveAsync(productId, cancellationToken));
    }
}
