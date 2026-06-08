using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.RolePermissions;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.OrganizationRoles)]
[Route("api/role-permissions")]
public sealed class RolePermissionsController : ControllerBase
{
    private readonly IRolePermissionService _service;

    public RolePermissionsController(IRolePermissionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<RolePermissionMatrixDto>> GetMatrix(CancellationToken cancellationToken) =>
        Ok(await _service.GetMatrixAsync(cancellationToken));

    [HttpPut("{roleId:guid}")]
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
