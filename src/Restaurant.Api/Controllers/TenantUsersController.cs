using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.TenantUsers;
using Restaurant.Infrastructure.Authorization;

namespace Restaurant.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{SystemRoles.Owner},{SystemRoles.Manager}")]
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
}
