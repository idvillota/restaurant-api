using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.TenantUsers;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.OrganizationTeam)]
[Route("api/[controller]")]
public sealed class TenantUsersController : ControllerBase
{
    private readonly ITenantUserInviteService _inviteService;

    public TenantUsersController(ITenantUserInviteService inviteService) => _inviteService = inviteService;

    /// <summary>Adds a user to the current tenant (new account or existing global user not yet in this tenant).</summary>
    [HttpPost("invite")]
    public async Task<ActionResult<InvitedTenantUserDto>> Invite(
        [FromBody] InviteTenantUserDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await _inviteService.InviteAsync(dto, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Members of the current tenant, each with a single primary role.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantUserMemberDto>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _inviteService.ListAsync(includeInactive, cancellationToken);
        return Ok(result);
    }

    /// <summary>Replaces all roles for the member with one assignable role (Manager, Waitress, or Cashier).</summary>
    [HttpPut("{tenantUserId:guid}/role")]
    public async Task<ActionResult<TenantUserMemberDto>> ChangeRole(
        Guid tenantUserId,
        [FromBody] ChangeTenantUserRoleDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await _inviteService.ChangeRoleAsync(tenantUserId, dto, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Enables or disables login for this tenant membership. Does not delete the user.</summary>
    [HttpPut("{tenantUserId:guid}/active")]
    public async Task<ActionResult<TenantUserMemberDto>> SetActive(
        Guid tenantUserId,
        [FromBody] SetTenantUserActiveDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _inviteService.SetActiveAsync(tenantUserId, dto.IsActive, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
