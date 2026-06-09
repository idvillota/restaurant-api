using Restaurant.Application.Authorization;

namespace Restaurant.Api.Authorization;

public static class FeatureAuthorizationExtensions
{
    public static IServiceCollection AddFeatureAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var code in FeatureCodes.All)
            {
                options.AddPolicy(
                    FeatureAuthorizationPolicies.For(code),
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireClaim(PermissionClaims.Type, code));
            }
        });

        return services;
    }
}
