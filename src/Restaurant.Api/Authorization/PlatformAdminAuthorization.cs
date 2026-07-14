using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Options;

namespace Restaurant.Api.Authorization;

public static class PlatformAuthorizationPolicies
{
    public const string PlatformAdmin = "PlatformAdmin";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequirePlatformAdminAttribute : AuthorizeAttribute
{
    public RequirePlatformAdminAttribute()
    {
        Policy = PlatformAuthorizationPolicies.PlatformAdmin;
    }
}

public sealed class PlatformAdminAuthorizationHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    private readonly PlatformOptions _options;

    public PlatformAdminAuthorizationHandler(IOptions<PlatformOptions> options)
    {
        _options = options.Value;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminRequirement requirement)
    {
        var email = ResolveEmail(context.User);
        if (string.IsNullOrWhiteSpace(email))
            return Task.CompletedTask;

        var allowed = _options.AdminEmails ?? [];
        if (allowed.Any(a => string.Equals(a.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private static string? ResolveEmail(ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? user.FindFirstValue(ClaimTypes.Email)
        ?? user.FindFirstValue("email");
}

public sealed class PlatformAdminRequirement : IAuthorizationRequirement;
