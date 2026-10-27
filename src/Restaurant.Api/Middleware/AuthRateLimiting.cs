using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Restaurant.Application.Common.Options;

namespace Restaurant.Api.Middleware;

internal static class AuthRateLimiting
{
    public const string LoginPolicy = "auth-login";
    public const string RegisterPolicy = "auth-register";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Demasiados intentos. Espere un momento e inténtelo de nuevo." },
                    cancellationToken);
            };

            options.AddPolicy(LoginPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = Math.Max(1, auth.LoginPermitLimit),
                        Window = TimeSpan.FromMinutes(Math.Max(1, auth.LoginWindowMinutes)),
                        QueueLimit = 0,
                    }));

            options.AddPolicy(RegisterPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = Math.Max(1, auth.RegisterPermitLimit),
                        Window = TimeSpan.FromMinutes(Math.Max(1, auth.RegisterWindowMinutes)),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    private static string GetClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
