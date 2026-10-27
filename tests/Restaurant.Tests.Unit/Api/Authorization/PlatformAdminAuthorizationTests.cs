using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Restaurant.Api.Authorization;
using Restaurant.Application.Common.Options;
using Restaurant.Infrastructure.Authorization;

namespace Restaurant.Tests.Unit.Api.Authorization;

public sealed class PlatformAdminAuthorizationTests
{
    private static PlatformAdminAuthorizationHandler Handler() =>
        new(Options.Create(new PlatformOptions { AdminEmails = ["ops@host.local"] }));

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user)
    {
        var handler = Handler();
        var context = new AuthorizationHandlerContext([new PlatformAdminRequirement()], user, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task Succeeds_for_platform_email_with_administrator_gral_role()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "ops@host.local"),
            new Claim(ClaimTypes.Role, SystemRoles.Administrator),
        ],
        "test"));

        Assert.True(await AuthorizeAsync(user));
    }

    [Fact]
    public async Task Fails_for_platform_email_with_administrator_manager_role()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "ops@host.local"),
            new Claim(ClaimTypes.Role, SystemRoles.Manager),
        ],
        "test"));

        Assert.False(await AuthorizeAsync(user));
    }

    [Fact]
    public async Task Fails_when_email_is_not_on_the_platform_list()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "owner@demo.local"),
            new Claim(ClaimTypes.Role, SystemRoles.Administrator),
        ],
        "test"));

        Assert.False(await AuthorizeAsync(user));
    }
}
