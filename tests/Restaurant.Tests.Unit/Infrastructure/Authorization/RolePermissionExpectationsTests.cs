using Restaurant.Application.Authorization;
using Restaurant.Infrastructure.Authorization;

namespace Restaurant.Tests.Unit.Infrastructure.Authorization;

/// <summary>
/// Guards the default role matrix against regressions in feature assignments and workflows.
/// </summary>
public sealed class RolePermissionExpectationsTests
{
    [Theory]
    [InlineData(SystemRoles.Administrator)]
    [InlineData(SystemRoles.Owner)]
    public void Admin_roles_receive_all_features(string roleName)
    {
        var permissions = FeatureCatalog.DefaultFeaturesByRole[roleName];
        Assert.Equal(FeatureCodes.All.Count, permissions.Count);
        foreach (var code in FeatureCodes.All)
            Assert.Contains(code, permissions);
    }

    [Fact]
    public void Waitress_can_operate_salon_without_catalog_admin()
    {
        var permissions = Permissions(SystemRoles.Waitress);

        Assert.Contains(FeatureCodes.ServiceSalon, permissions);
        Assert.Contains(FeatureCodes.ReservationsManage, permissions);
        Assert.Contains(FeatureCodes.CustomersManage, permissions);
        Assert.DoesNotContain(FeatureCodes.CatalogProducts, permissions);
        Assert.DoesNotContain(FeatureCodes.CatalogProductTypes, permissions);
        Assert.DoesNotContain(FeatureCodes.PaymentsCheckout, permissions);
        Assert.DoesNotContain(FeatureCodes.CashierShifts, permissions);
        Assert.DoesNotContain(FeatureCodes.OrganizationRoles, permissions);
    }

    [Fact]
    public void Cashier_can_checkout_and_view_sales_reports()
    {
        var permissions = Permissions(SystemRoles.Cashier);

        Assert.Contains(FeatureCodes.ServiceSalon, permissions);
        Assert.Contains(FeatureCodes.PaymentsCheckout, permissions);
        Assert.Contains(FeatureCodes.CashierShifts, permissions);
        Assert.Contains(FeatureCodes.ReportsSales, permissions);
        Assert.Contains(FeatureCodes.ReportsSalesByDate, permissions);
        Assert.DoesNotContain(FeatureCodes.CatalogProducts, permissions);
        Assert.DoesNotContain(FeatureCodes.ReservationsManage, permissions);
        Assert.DoesNotContain(FeatureCodes.OrganizationRoles, permissions);
    }

    [Fact]
    public void Manager_runs_operations_but_not_roles_or_strategic_ai()
    {
        var permissions = Permissions(SystemRoles.Manager);

        Assert.Contains(FeatureCodes.DashboardConfigure, permissions);
        Assert.Contains(FeatureCodes.ServiceSalon, permissions);
        Assert.Contains(FeatureCodes.PaymentsCheckout, permissions);
        Assert.Contains(FeatureCodes.CashierShifts, permissions);
        Assert.Contains(FeatureCodes.TablesManage, permissions);
        Assert.Contains(FeatureCodes.CatalogProducts, permissions);
        Assert.Contains(FeatureCodes.SettingsTenant, permissions);
        Assert.Contains(FeatureCodes.OrganizationTeam, permissions);
        Assert.DoesNotContain(FeatureCodes.OrganizationRoles, permissions);
        Assert.DoesNotContain(FeatureCodes.ReportsStrategicAi, permissions);
    }

    [Fact]
    public void Every_default_role_includes_dashboard_view()
    {
        foreach (var roleName in SystemRoles.All)
        {
            var permissions = Permissions(roleName);
            Assert.Contains(FeatureCodes.DashboardView, permissions);
        }
    }

    [Fact]
    public void Default_features_reference_known_catalog_codes()
    {
        var known = FeatureCodes.All.ToHashSet(StringComparer.Ordinal);

        foreach (var (role, codes) in FeatureCatalog.DefaultFeaturesByRole)
        {
            foreach (var code in codes)
                Assert.True(known.Contains(code), $"Role '{role}' references unknown feature '{code}'.");
        }
    }

    private static HashSet<string> Permissions(string roleName) =>
        FeatureCatalog.DefaultFeaturesByRole[roleName].ToHashSet(StringComparer.Ordinal);
}
