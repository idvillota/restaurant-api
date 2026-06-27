namespace Restaurant.Application.Features.Dashboard;

public static class DefaultDashboardLayout
{
    public static DashboardLayoutDto Create() =>
        new()
        {
            Version = 1,
            Panels =
            [
                new DashboardPanelDto
                {
                    Id = "quick-links",
                    WidgetType = "quick_links",
                    X = 0,
                    Y = 0,
                    W = 12,
                    H = 3,
                },
                new DashboardPanelDto
                {
                    Id = "sales-kpis",
                    WidgetType = "sales_kpis",
                    X = 0,
                    Y = 3,
                    W = 4,
                    H = 2,
                    Config = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["days"] = System.Text.Json.JsonSerializer.SerializeToElement(7),
                    },
                },
                new DashboardPanelDto
                {
                    Id = "cashier-status",
                    WidgetType = "cashier_status",
                    X = 4,
                    Y = 3,
                    W = 4,
                    H = 2,
                },
                new DashboardPanelDto
                {
                    Id = "salon-tables",
                    WidgetType = "salon_tables",
                    X = 8,
                    Y = 3,
                    W = 4,
                    H = 2,
                },
                new DashboardPanelDto
                {
                    Id = "sales-trend",
                    WidgetType = "sales_trend",
                    X = 0,
                    Y = 5,
                    W = 8,
                    H = 4,
                    Config = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["days"] = System.Text.Json.JsonSerializer.SerializeToElement(7),
                    },
                },
                new DashboardPanelDto
                {
                    Id = "low-stock",
                    WidgetType = "ingredients_low_stock",
                    X = 8,
                    Y = 5,
                    W = 4,
                    H = 4,
                    Config = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["limit"] = System.Text.Json.JsonSerializer.SerializeToElement(8),
                    },
                },
            ],
        };
}
