namespace Restaurant.Application.Authorization;

public static class FeatureCodes
{
    public const string DashboardView = "dashboard.view";
    public const string ServiceSalon = "service.salon";
    public const string PaymentsCheckout = "payments.checkout";
    public const string ReservationsManage = "reservations.manage";
    public const string CustomersManage = "customers.manage";
    public const string TablesManage = "tables.manage";
    public const string CatalogProducts = "catalog.products";
    public const string CatalogProductTypes = "catalog.product-types";
    public const string CatalogIngredientCategories = "catalog.ingredient-categories";
    public const string CatalogIngredients = "catalog.ingredients";
    public const string SettingsTenant = "settings.tenant";
    public const string ProcurementPurchases = "procurement.purchases";
    public const string ProcurementProviders = "procurement.providers";
    public const string OrganizationEmployees = "organization.employees";
    public const string OrganizationTeam = "organization.team";
    public const string OrganizationRoles = "organization.roles";
    public const string CashierShifts = "cashier.shifts";
    public const string ReportsDailyClosure = "reports.daily_closure";

    public static IReadOnlyList<string> All { get; } =
    [
        DashboardView,
        ServiceSalon,
        PaymentsCheckout,
        ReservationsManage,
        CustomersManage,
        TablesManage,
        CatalogProducts,
        CatalogProductTypes,
        CatalogIngredientCategories,
        CatalogIngredients,
        SettingsTenant,
        ProcurementPurchases,
        ProcurementProviders,
        OrganizationEmployees,
        OrganizationTeam,
        OrganizationRoles,
        CashierShifts,
        ReportsDailyClosure,
    ];
}
