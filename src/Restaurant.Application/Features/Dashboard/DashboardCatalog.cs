using Restaurant.Application.Authorization;

namespace Restaurant.Application.Features.Dashboard;

public static class DashboardCatalog
{
    public static readonly DashboardWidgetDefinitionDto[] Widgets =
    [
        new()
        {
            WidgetType = "quick_links",
            Name = "Accesos rápidos",
            Description = "Enlaces a las áreas más usadas del sistema.",
            Category = "General",
            RequiredPermission = FeatureCodes.DashboardView,
            DefaultWidth = 12,
            DefaultHeight = 3,
            MinWidth = 4,
            MinHeight = 2,
        },
        new()
        {
            WidgetType = "sales_kpis",
            Name = "Ventas del período",
            Description = "Totales de ventas, compras y resultado neto.",
            Category = "Reportes",
            RequiredPermission = FeatureCodes.ReportsSalesByDate,
            DefaultWidth = 4,
            DefaultHeight = 2,
            MinWidth = 3,
            MinHeight = 2,
        },
        new()
        {
            WidgetType = "sales_trend",
            Name = "Tendencia de ventas",
            Description = "Gráfico de ventas vs compras por día.",
            Category = "Reportes",
            RequiredPermission = FeatureCodes.ReportsSalesByDate,
            DefaultWidth = 8,
            DefaultHeight = 4,
            MinWidth = 4,
            MinHeight = 3,
        },
        new()
        {
            WidgetType = "ingredients_low_stock",
            Name = "Stock bajo",
            Description = "Ingredientes por debajo del nivel de reorden.",
            Category = "Inventario",
            RequiredPermission = FeatureCodes.ReportsIngredients,
            DefaultWidth = 4,
            DefaultHeight = 4,
            MinWidth = 3,
            MinHeight = 3,
        },
        new()
        {
            WidgetType = "cashier_status",
            Name = "Día operativo y caja",
            Description = "Fecha operativa, cierre diario y turnos abiertos.",
            Category = "Servicio",
            RequiredPermission = FeatureCodes.CashierShifts,
            DefaultWidth = 4,
            DefaultHeight = 2,
            MinWidth = 3,
            MinHeight = 2,
        },
        new()
        {
            WidgetType = "salon_tables",
            Name = "Estado del salón",
            Description = "Mesas ocupadas y pedidos activos.",
            Category = "Servicio",
            RequiredPermission = FeatureCodes.ServiceSalon,
            DefaultWidth = 4,
            DefaultHeight = 2,
            MinWidth = 3,
            MinHeight = 2,
        },
    ];

    public static IReadOnlyDictionary<string, DashboardWidgetDefinitionDto> ByType { get; } =
        Widgets.ToDictionary(w => w.WidgetType, StringComparer.Ordinal);
}
