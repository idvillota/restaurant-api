using Restaurant.Application.Authorization;

namespace Restaurant.Infrastructure.Authorization;

public sealed record FeatureDefinition(Guid Id, string Code, string Name, string Module, int SortOrder);

public static class FeatureCatalog
{
    public static readonly FeatureDefinition[] All =
    [
        new(Guid.Parse("f1000001-0001-4001-8001-000000000001"), FeatureCodes.DashboardView, "Panel", "General", 10),
        new(Guid.Parse("f1000002-0002-4002-8002-000000000002"), FeatureCodes.ServiceSalon, "Salón", "Servicio", 20),
        new(Guid.Parse("f1000003-0003-4003-8003-000000000003"), FeatureCodes.PaymentsCheckout, "Pagos", "Servicio", 30),
        new(Guid.Parse("f1000004-0004-4004-8004-000000000004"), FeatureCodes.ReservationsManage, "Reservas", "Servicio", 40),
        new(Guid.Parse("f1000005-0005-4005-8005-000000000005"), FeatureCodes.CustomersManage, "Clientes", "Servicio", 50),
        new(Guid.Parse("f1000006-0006-4006-8006-000000000006"), FeatureCodes.TablesManage, "Mesas", "Servicio", 60),
        new(Guid.Parse("f1000007-0007-4007-8007-000000000007"), FeatureCodes.CatalogProducts, "Productos", "Menú", 70),
        new(Guid.Parse("f1000008-0008-4008-8008-000000000008"), FeatureCodes.CatalogProductTypes, "Tipos de producto", "Menú", 80),
        new(Guid.Parse("f1000009-0009-4009-8009-000000000009"), FeatureCodes.CatalogIngredientCategories, "Categorías de ingrediente", "Menú", 90),
        new(Guid.Parse("f100000a-0010-4010-8010-00000000000a"), FeatureCodes.CatalogIngredients, "Ingredientes", "Menú", 100),
        new(Guid.Parse("f1000014-0020-4020-8020-000000000014"), FeatureCodes.CatalogPublicMenuQr, "Código QR menú", "Menú", 105),
        new(Guid.Parse("f100000b-0011-4011-8011-00000000000b"), FeatureCodes.SettingsTenant, "Configuración", "Administración", 110),
        new(Guid.Parse("f100000c-0012-4012-8012-00000000000c"), FeatureCodes.ProcurementPurchases, "Compras", "Administración", 120),
        new(Guid.Parse("f100000d-0013-4013-8013-00000000000d"), FeatureCodes.ProcurementProviders, "Proveedores", "Administración", 130),
        new(Guid.Parse("f100000e-0014-4014-8014-00000000000e"), FeatureCodes.OrganizationEmployees, "Empleados", "Administración", 140),
        new(Guid.Parse("f100000f-0015-4015-8015-00000000000f"), FeatureCodes.OrganizationTeam, "Invitar equipo", "Administración", 150),
        new(Guid.Parse("f1000010-0016-4016-8016-000000000010"), FeatureCodes.OrganizationRoles, "Roles y permisos", "Administración", 160),
        new(Guid.Parse("f1000011-0017-4017-8017-000000000011"), FeatureCodes.CashierShifts, "Turnos de caja", "Servicio", 35),
        new(Guid.Parse("f1000012-0018-4018-8018-000000000012"), FeatureCodes.ReportsDailyClosure, "Cierre diario", "Administración", 165),
        new(Guid.Parse("f1000013-0019-4019-8019-000000000013"), FeatureCodes.ReportsStrategicAi, "Informe IA", "Administración", 170),
    ];

    public static IReadOnlyDictionary<string, Guid> IdsByCode { get; } =
        All.ToDictionary(f => f.Code, f => f.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultFeaturesByRole { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [SystemRoles.Administrator] = FeatureCodes.All,
            [SystemRoles.Owner] = FeatureCodes.All,
            [SystemRoles.Manager] =
            [
                FeatureCodes.DashboardView,
                FeatureCodes.ServiceSalon,
                FeatureCodes.PaymentsCheckout,
                FeatureCodes.CashierShifts,
                FeatureCodes.ReservationsManage,
                FeatureCodes.CustomersManage,
                FeatureCodes.TablesManage,
                FeatureCodes.CatalogProducts,
                FeatureCodes.CatalogProductTypes,
                FeatureCodes.CatalogIngredientCategories,
                FeatureCodes.CatalogIngredients,
                FeatureCodes.CatalogPublicMenuQr,
                FeatureCodes.SettingsTenant,
                FeatureCodes.ProcurementPurchases,
                FeatureCodes.ProcurementProviders,
                FeatureCodes.OrganizationEmployees,
                FeatureCodes.OrganizationTeam,
                FeatureCodes.ReportsDailyClosure,
            ],
            [SystemRoles.Waitress] =
            [
                FeatureCodes.DashboardView,
                FeatureCodes.ServiceSalon,
                FeatureCodes.ReservationsManage,
                FeatureCodes.CustomersManage,
                FeatureCodes.CatalogPublicMenuQr,
            ],
            [SystemRoles.Staff] =
            [
                FeatureCodes.DashboardView,
                FeatureCodes.ServiceSalon,
                FeatureCodes.ReservationsManage,
                FeatureCodes.CustomersManage,
                FeatureCodes.CatalogPublicMenuQr,
            ],
            [SystemRoles.Cashier] =
            [
                FeatureCodes.DashboardView,
                FeatureCodes.ServiceSalon,
                FeatureCodes.PaymentsCheckout,
                FeatureCodes.CashierShifts,
                FeatureCodes.CustomersManage,
                FeatureCodes.CatalogPublicMenuQr,
            ],
        };
}
