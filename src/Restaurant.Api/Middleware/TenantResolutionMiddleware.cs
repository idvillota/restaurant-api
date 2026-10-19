using System.Security.Claims;
using System.Text.Json;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Api.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task InvokeAsync(HttpContext context, ICurrentTenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(tenantClaim, out var tenantId) && tenantId != Guid.Empty)
                tenantContext.TenantId = tenantId;

            var userSub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userSub, out var userId))
                tenantContext.UserId = userId;
        }

        if (RequiresTenant(context) &&
            (tenantContext.TenantId is null || tenantContext.TenantId == Guid.Empty))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new
                    {
                        code = "tenant_required",
                        message = "Sesión inválida: no se pudo determinar el local (tenant). Vuelva a iniciar sesión.",
                    },
                    JsonOptions));
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Authenticated tenant-data APIs must have a valid tenant. Auth, health, and public menu are exempt.
    /// </summary>
    public static bool RequiresTenant(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return false;

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Length == 0)
            return false;

        // CORS preflight
        if (HttpMethods.IsOptions(context.Request.Method))
            return false;

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.StartsWith("/api/public/", StringComparison.OrdinalIgnoreCase))
            return false;

        // Platform ops are cross-tenant; handlers use IgnoreQueryFilters + platform auth.
        if (path.StartsWith("/api/platform/", StringComparison.OrdinalIgnoreCase))
            return false;

        // Swagger UI / OpenAPI (local/dev)
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
