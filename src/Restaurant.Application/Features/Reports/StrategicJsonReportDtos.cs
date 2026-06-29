namespace Restaurant.Application.Features.Reports;

public static class StrategicReportTypes
{
    public const string MenuEngineering = "menu_engineering";
    public const string SalesForecast = "sales_forecast";
    public const string IngredientForecast = "ingredient_forecast";
    public const string FoodCostMargin = "food_cost_margin";
    public const string SupplierAbc = "supplier_abc";
    public const string ProductMixByHour = "product_mix_by_hour";
    public const string LegacyAi = "legacy_ai";

    public static IReadOnlyList<string> All { get; } =
    [
        MenuEngineering,
        SalesForecast,
        IngredientForecast,
        FoodCostMargin,
        SupplierAbc,
        ProductMixByHour,
        LegacyAi,
    ];
}

public sealed class StrategicJsonReportDocument
{
    public required StrategicJsonReportMeta Meta { get; set; }
    public required StrategicJsonReportSummary Summary { get; set; }
    public IReadOnlyList<StrategicJsonReportSection> Sections { get; set; } = [];
}

public sealed class StrategicJsonReportMeta
{
    public required string ReportType { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public bool FromCache { get; set; }
    public int? ForecastDays { get; set; }
}

public sealed class StrategicJsonReportSummary
{
    public IReadOnlyList<StrategicJsonKpi> Kpis { get; set; } = [];
    public IReadOnlyList<string> Insights { get; set; } = [];
    public IReadOnlyList<string> Recommendations { get; set; } = [];
}

public sealed class StrategicJsonKpi
{
    public required string Id { get; set; }
    public required string Label { get; set; }
    public required string Value { get; set; }
    public string? Hint { get; set; }
    public string Tone { get; set; } = "default";
}

public sealed class StrategicJsonReportSection
{
    public required string Type { get; set; }
    public required string Title { get; set; }
    public string? Content { get; set; }
    public IReadOnlyList<StrategicJsonTableColumn>? Columns { get; set; }
    public IReadOnlyList<Dictionary<string, object?>>? Rows { get; set; }
    public string? ChartType { get; set; }
    public string? ValueLabel { get; set; }
    public string? CategoryLabel { get; set; }
    public IReadOnlyList<StrategicJsonChartPoint>? Points { get; set; }
    public IReadOnlyList<StrategicJsonChartSeries>? Series { get; set; }
    public string? XAxisLabel { get; set; }
    public string? YAxisLabel { get; set; }
    public decimal? XThreshold { get; set; }
    public decimal? YThreshold { get; set; }
    public IReadOnlyList<StrategicJsonMatrixItem>? Items { get; set; }
    public string? AlertTone { get; set; }
}

public sealed class StrategicJsonTableColumn
{
    public required string Key { get; set; }
    public required string Label { get; set; }
    public string Align { get; set; } = "left";
    public string? Format { get; set; }
}

public sealed class StrategicJsonChartPoint
{
    public required string Category { get; set; }
    public decimal Value { get; set; }
    public string? Detail { get; set; }
    public string? SeriesKey { get; set; }
}

public sealed class StrategicJsonChartSeries
{
    public required string Key { get; set; }
    public required string Label { get; set; }
    public string Color { get; set; } = "#228be6";
}

public sealed class StrategicJsonMatrixItem
{
    public required string Label { get; set; }
    public required string Quadrant { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public string? Detail { get; set; }
}

public sealed class StrategicAiInsightsDto
{
    public IReadOnlyList<string> Insights { get; set; } = [];
    public IReadOnlyList<string> Recommendations { get; set; } = [];
}
