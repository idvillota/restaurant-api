using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/menu")]
public sealed class PublicMenuController : ControllerBase
{
    private readonly IPublicMenuService _publicMenu;

    public PublicMenuController(IPublicMenuService publicMenu) => _publicMenu = publicMenu;

    [HttpGet("{tenantSlug}")]
    public async Task<IActionResult> GetByTenantSlug(string tenantSlug, CancellationToken cancellationToken)
    {
        var menu = await _publicMenu.GetByTenantSlugAsync(tenantSlug, cancellationToken);
        if (menu is null)
            return NotFound(new { message = "Menu not found." });

        return Ok(menu);
    }
}
