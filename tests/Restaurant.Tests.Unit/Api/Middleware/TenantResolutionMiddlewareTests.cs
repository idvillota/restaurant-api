using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Restaurant.Api.Middleware;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Tests.Unit.Api.Middleware;

public sealed class TenantResolutionMiddlewareTests
{
    [Theory]
    [InlineData("/api/Products", true)]
    [InlineData("/api/products", true)]
    [InlineData("/api/Auth/login", false)]
    [InlineData("/api/auth/register", false)]
    [InlineData("/api/public/menu/demo", false)]
    [InlineData("/api/platform/tenants/initial-data-template", false)]
    [InlineData("/health", false)]
    [InlineData("/swagger/index.html", false)]
    public void RequiresTenant_for_authenticated_paths(string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"));

        Assert.Equal(expected, TenantResolutionMiddleware.RequiresTenant(context));
    }

    [Fact]
    public void RequiresTenant_false_when_anonymous()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/Products";
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(TenantResolutionMiddleware.RequiresTenant(context));
    }

    [Fact]
    public async Task Invoke_returns_401_when_authenticated_without_tenant_claim()
    {
        var tenantContext = new CurrentTenantContext();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/Products";
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "Bearer"));

        await middleware.InvokeAsync(context, tenantContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Null(tenantContext.TenantId);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("tenant_required", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_sets_tenant_and_continues_when_claim_present()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new CurrentTenantContext();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/Products";
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim("tenant_id", tenantId.ToString()),
                ],
                authenticationType: "Bearer"));

        await middleware.InvokeAsync(context, tenantContext);

        Assert.True(nextCalled);
        Assert.Equal(tenantId, tenantContext.TenantId);
    }
}
