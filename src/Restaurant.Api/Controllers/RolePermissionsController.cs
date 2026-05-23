using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.RolePermissions;
using Restaurant.Infrastructure.Authorization;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/role-permissions")]
public sealed class RolePermissionsController : ControllerBase
{
    private readonly IRolePermissionService _service;

    public RolePermissionsController(IRolePermissionService service) => _service = service;

    [HttpGet]
    [Authorize(Roles = $"{SystemRoles.Administrator},{SystemRoles.Owner},{SystemRoles.Manager}")]
    public async Task<ActionResult<RolePermissionMatrixDto>> GetMatrix(CancellationToken cancellationToken) =>
        Ok(await _service.GetMatrixAsync(cancellationToken));

    [HttpPut("{roleId:guid}")]
    [Authorize(Roles = $"{SystemRoles.Administrator},{SystemRoles.Owner}")]
    public async Task<IActionResult> UpdateRole(
        Guid roleId,
        [FromBody] UpdateRolePermissionsDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            await _service.UpdateRolePermissionsAsync(roleId, dto, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
