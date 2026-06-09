using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Authorization;
using Restaurant.Api.Controllers;
using Restaurant.Application.Authorization;
using Restaurant.Infrastructure.Identity;

namespace Restaurant.Tests.Unit.Api.Authorization;

public sealed class FeatureAuthorizationTests
{
    [Fact]
    public void FeatureAuthorizationPolicies_uses_stable_prefix()
    {
        var policy = FeatureAuthorizationPolicies.For(FeatureCodes.CatalogProducts);
        Assert.Equal("Feature:catalog.products", policy);
    }

    [Fact]
    public void PermissionClaims_matches_jwt_token_service()
    {
        Assert.Equal(JwtTokenService.PermissionClaimType, PermissionClaims.Type);
    }

    [Fact]
    public async Task AddFeatureAuthorization_registers_policy_per_feature_code()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFeatureAuthorization();

        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(
            FeatureAuthorizationPolicies.For(FeatureCodes.PaymentsCheckout));

        Assert.NotNull(policy);
        Assert.NotEmpty(policy!.Requirements);
    }

    [Theory]
    [InlineData(typeof(ProductsController), FeatureCodes.CatalogProducts)]
    [InlineData(typeof(BillsController), FeatureCodes.PaymentsCheckout)]
    [InlineData(typeof(SalesOrdersController), FeatureCodes.ServiceSalon)]
    [InlineData(typeof(TenantUsersController), FeatureCodes.OrganizationTeam)]
    [InlineData(typeof(RolePermissionsController), FeatureCodes.OrganizationRoles)]
    [InlineData(typeof(StrategicReportsController), FeatureCodes.ReportsStrategicAi)]
    public void Secured_controllers_require_matching_feature(Type controllerType, string featureCode)
    {
        var attribute = controllerType.GetCustomAttribute<RequireFeatureAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(FeatureAuthorizationPolicies.For(featureCode), attribute!.Policy);
    }

    [Fact]
    public void PublicMenuController_allows_anonymous()
    {
        Assert.NotNull(typeof(PublicMenuController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
    }
}
