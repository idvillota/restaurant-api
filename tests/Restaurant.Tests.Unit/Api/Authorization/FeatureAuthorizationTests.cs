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
    public async Task Salon_catalog_read_policies_allow_service_salon_or_catalog_permissions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFeatureAuthorization();

        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var evaluator = provider.GetRequiredService<IAuthorizationService>();

        var productsPolicy = await policyProvider.GetPolicyAsync(
            FeatureAuthorizationPolicies.SalonCatalogProductsRead);
        Assert.NotNull(productsPolicy);

        var waitress = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionClaims.Type, FeatureCodes.ServiceSalon),
        ],
        authenticationType: "test"));

        var admin = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionClaims.Type, FeatureCodes.CatalogProducts),
        ],
        authenticationType: "test"));

        var denied = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionClaims.Type, FeatureCodes.DashboardView),
        ],
        authenticationType: "test"));

        Assert.Equal(
            AuthorizationResult.Success().Succeeded,
            (await evaluator.AuthorizeAsync(waitress, productsPolicy!)).Succeeded);
        Assert.Equal(
            AuthorizationResult.Success().Succeeded,
            (await evaluator.AuthorizeAsync(admin, productsPolicy!)).Succeeded);
        Assert.False((await evaluator.AuthorizeAsync(denied, productsPolicy!)).Succeeded);
    }

    [Fact]
    public async Task Operational_cashier_context_policy_allows_payments_without_shift_management()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFeatureAuthorization();

        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var evaluator = provider.GetRequiredService<IAuthorizationService>();

        var policy = await policyProvider.GetPolicyAsync(
            FeatureAuthorizationPolicies.OperationalCashierContextRead);
        Assert.NotNull(policy);

        var cashier = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionClaims.Type, FeatureCodes.PaymentsCheckout),
        ],
        authenticationType: "test"));

        var shiftManager = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionClaims.Type, FeatureCodes.CashierShifts),
        ],
        authenticationType: "test"));

        var denied = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionClaims.Type, FeatureCodes.ServiceSalon),
        ],
        authenticationType: "test"));

        Assert.True((await evaluator.AuthorizeAsync(cashier, policy!)).Succeeded);
        Assert.True((await evaluator.AuthorizeAsync(shiftManager, policy!)).Succeeded);
        Assert.False((await evaluator.AuthorizeAsync(denied, policy!)).Succeeded);
    }

    [Fact]
    public void PublicMenuController_allows_anonymous()
    {
        Assert.NotNull(typeof(PublicMenuController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
    }
}
