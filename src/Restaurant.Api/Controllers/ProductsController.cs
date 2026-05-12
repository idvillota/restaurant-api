using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductReadService _products;

    public ProductsController(IProductReadService products) => _products = products;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await _products.ListAsync(cancellationToken);
        return Ok(items);
    }
}
