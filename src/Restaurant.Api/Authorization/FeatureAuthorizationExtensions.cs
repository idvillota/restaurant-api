using Microsoft.AspNetCore.Authorization;
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

            options.AddPolicy(
                FeatureAuthorizationPolicies.SalonCatalogProductsRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        HasPermission(context, FeatureCodes.ServiceSalon)
                        || HasPermission(context, FeatureCodes.CatalogProducts)));

            options.AddPolicy(
                FeatureAuthorizationPolicies.SalonCatalogProductTypesRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        HasPermission(context, FeatureCodes.ServiceSalon)
                        || HasPermission(context, FeatureCodes.CatalogProductTypes)));

            options.AddPolicy(
                FeatureAuthorizationPolicies.OperationalCashierContextRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        HasPermission(context, FeatureCodes.CashierShifts)
                        || HasPermission(context, FeatureCodes.PaymentsCheckout)
                        || HasPermission(context, FeatureCodes.ReportsDailyClosure)));
        });

        return services;
    }

    private static bool HasPermission(AuthorizationHandlerContext context, string featureCode) =>
        context.User.HasClaim(c =>
            c.Type == PermissionClaims.Type
            && c.Value.Equals(featureCode, StringComparison.Ordinal));
}
