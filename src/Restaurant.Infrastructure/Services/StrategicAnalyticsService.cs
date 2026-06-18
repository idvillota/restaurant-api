using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Reports;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class StrategicAnalyticsService : IStrategicAnalyticsService
{
    private const int DefaultForecastDays = 7;
    private const int MaxForecastDays = 30;
    private const int MaxDateSpanDays = 366;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenant;
    private readonly IStrategicAiInsightService _aiInsights;
    private readonly ILogger<StrategicAnalyticsService> _logger;

    public StrategicAnalyticsService(
        ApplicationDbContext db,
        ICurrentTenantContext tenant,
        IStrategicAiInsightService aiInsights,
        ILogger<StrategicAnalyticsService> logger)
    {
        _db = db;
        _tenant = tenant;
        _aiInsights = aiInsights;
        _logger = logger;
    }

    public async Task<StrategicJsonReportDocument> GetReportAsync(
        string reportType,
        DateOnly startDate,
        DateOnly endDate,
        int? forecastDays,
        bool refresh,
        bool includeAiInsights,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(startDate, endDate);
        var normalizedType = NormalizeReportType(reportType);
        var effectiveForecastDays = NormalizeForecastDays(forecastDays, normalizedType);

        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No se pudo determinar el local activo.");

        var cacheDate = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!refresh)
        {
            var cached = await TryLoadCacheAsync(
                tenantId,
                normalizedType,
                startDate,
                endDate,
                effectiveForecastDays,
                cacheDate,
                cancellationToken);

            if (cached is not null)
                return cached;
        }

        var document = normalizedType switch
        {
            StrategicReportTypes.MenuEngineering => await BuildMenuEngineeringAsync(
                tenantId, startDate, endDate, cancellationToken),
            StrategicReportTypes.SalesForecast => await BuildSalesForecastAsync(
                tenantId, startDate, endDate, effectiveForecastDays!.Value, cancellationToken),
            StrategicReportTypes.IngredientForecast => await BuildIngredientForecastAsync(
                tenantId, startDate, endDate, effectiveForecastDays!.Value, cancellationToken),
            StrategicReportTypes.FoodCostMargin => await BuildFoodCostMarginAsync(
                tenantId, startDate, endDate, cancellationToken),
            StrategicReportTypes.SupplierAbc => await BuildSupplierAbcAsync(
                tenantId, startDate, endDate, cancellationToken),
            StrategicReportTypes.ProductMixByHour => await BuildProductMixByHourAsync(
                tenantId, startDate, endDate, cancellationToken),
            _ => throw new InvalidOperationException($"Tipo de informe no soportado: {reportType}"),
        };

        document.Meta.FromCache = false;
        document.Meta.GeneratedAtUtc = DateTime.UtcNow;

        if (includeAiInsights)
        {
            try
            {
                var insights = await _aiInsights.GenerateInsightsAsync(document, cancellationToken);
                document.Summary.Insights = insights.Insights;
                document.Summary.Recommendations = insights.Recommendations;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron generar insights de IA para {ReportType}", normalizedType);
            }
        }

        await SaveCacheAsync(
            tenantId,
            normalizedType,
            startDate,
            endDate,
            effectiveForecastDays,
            cacheDate,
            document,
            cancellationToken);

        return document;
    }

    private static void ValidateDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            throw new InvalidOperationException("La fecha final debe ser igual o posterior a la fecha inicial.");

        if (endDate.DayNumber - startDate.DayNumber > MaxDateSpanDays)
            throw new InvalidOperationException($"El rango no puede superar {MaxDateSpanDays} días.");
    }

    private static string NormalizeReportType(string reportType)
    {
        var normalized = reportType.Trim().ToLowerInvariant().Replace('-', '_');
        if (!StrategicReportTypes.All.Contains(normalized) || normalized == StrategicReportTypes.LegacyAi)
            throw new InvalidOperationException($"Tipo de informe no soportado: {reportType}");

        return normalized;
    }

    private static int? NormalizeForecastDays(int? forecastDays, string reportType)
    {
        if (reportType is not StrategicReportTypes.SalesForecast and not StrategicReportTypes.IngredientForecast)
            return null;

        var days = forecastDays ?? DefaultForecastDays;
        if (days < 1 || days > MaxForecastDays)
            throw new InvalidOperationException($"El horizonte de pronóstico debe estar entre 1 y {MaxForecastDays} días.");

        return days;
    }

    private async Task<StrategicJsonReportDocument?> TryLoadCacheAsync(
        Guid tenantId,
        string reportType,
        DateOnly startDate,
        DateOnly endDate,
        int? forecastDays,
        DateOnly cacheDate,
        CancellationToken cancellationToken)
    {
        var cached = await _db.StrategicAiReportCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c =>
                    c.TenantId == tenantId
                    && c.ReportType == reportType
                    && c.SalesStartDate == startDate
                    && c.SalesEndDate == endDate
                    && c.ForecastDays == forecastDays
                    && c.CacheDate == cacheDate,
                cancellationToken);

        if (cached is null)
            return null;

        try
        {
            var document = JsonSerializer.Deserialize<StrategicJsonReportDocument>(cached.HtmlContent, JsonOptions);
            if (document is null)
                return null;

            document.Meta.FromCache = true;
            document.Meta.GeneratedAtUtc = cached.GeneratedAtUtc;
            return document;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Caché de informe estratégico corrupta para {ReportType}", reportType);
            return null;
        }
    }

    private async Task SaveCacheAsync(
        Guid tenantId,
        string reportType,
        DateOnly startDate,
        DateOnly endDate,
        int? forecastDays,
        DateOnly cacheDate,
        StrategicJsonReportDocument document,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var generatedAtUtc = document.Meta.GeneratedAtUtc;

        var existing = await _db.StrategicAiReportCaches.FirstOrDefaultAsync(
            c =>
                c.TenantId == tenantId
                && c.ReportType == reportType
                && c.SalesStartDate == startDate
                && c.SalesEndDate == endDate
                && c.ForecastDays == forecastDays
                && c.CacheDate == cacheDate,
            cancellationToken);

        if (existing is null)
        {
            await _db.StrategicAiReportCaches.AddAsync(
                new StrategicAiReportCache
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ReportType = reportType,
                    SalesStartDate = startDate,
                    SalesEndDate = endDate,
                    ForecastDays = forecastDays,
                    CacheDate = cacheDate,
                    HtmlContent = json,
                    GeneratedAtUtc = generatedAtUtc,
                    CreatedAtUtc = generatedAtUtc,
                },
                cancellationToken);
        }
        else
        {
            existing.HtmlContent = json;
            existing.GeneratedAtUtc = generatedAtUtc;
            existing.UpdatedAtUtc = generatedAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<SalesLineRow>> LoadPaidSalesLinesAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var lines = await _db.SalesOrderLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId
                && l.CreatedAtUtc >= startUtc
                && l.CreatedAtUtc < endExclusive
                && l.SalesOrder.Status == SalesOrderStatus.Paid)
            .Select(l => new SalesLineRow(
                l.ProductId,
                l.Product.Name,
                l.Product.ProductType.Name,
                l.Quantity,
                l.LineTotal,
                l.UnitCostPrice,
                l.SalesOrderId,
                l.CreatedAtUtc,
                l.Id))
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
            return lines;

        var lineIds = lines.Select(l => l.LineId).ToList();
        var exclusions = await _db.SalesOrderLineExcludedIngredients
            .AsNoTracking()
            .Where(e => lineIds.Contains(e.SalesOrderLineId))
            .Select(e => new { e.SalesOrderLineId, e.IngredientId })
            .ToListAsync(cancellationToken);

        var excludedByLine = exclusions
            .GroupBy(e => e.SalesOrderLineId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.IngredientId).ToHashSet());

        return lines
            .Select(l => l with
            {
                ExcludedIngredientIds = excludedByLine.GetValueOrDefault(l.LineId) ?? [],
            })
            .ToList();
    }

    private async Task<StrategicJsonReportDocument> BuildMenuEngineeringAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var lines = await LoadPaidSalesLinesAsync(tenantId, startDate, endDate, cancellationToken);
        var fallbackCosts = await LoadFallbackUnitCostsAsync(lines, cancellationToken);

        var aggregates = lines
            .GroupBy(l => l.ProductId)
            .Select(g =>
            {
                var first = g.First();
                var qty = g.Sum(x => x.Quantity);
                var revenue = g.Sum(x => x.LineTotal);
                var cogs = g.Sum(x => LineCogs(x, fallbackCosts));
                var margin = revenue - cogs;
                var marginPct = revenue > 0 ? margin / revenue * 100m : 0m;
                return new MenuItemAggregate(
                    g.Key,
                    first.ProductName,
                    first.ProductTypeName,
                    qty,
                    revenue,
                    margin,
                    marginPct);
            })
            .Where(x => x.Quantity > 0)
            .ToList();

        var totalQty = aggregates.Sum(x => x.Quantity);
        var totalRevenue = aggregates.Sum(x => x.Revenue);
        var medianQty = Median(aggregates.Select(x => x.Quantity));
        var medianMarginPct = Median(aggregates.Select(x => x.MarginPercent));

        var classified = aggregates
            .Select(x =>
            {
                var mixPct = totalQty > 0 ? x.Quantity / totalQty * 100m : 0m;
                var popular = x.Quantity >= medianQty;
                var profitable = x.MarginPercent >= medianMarginPct;
                var quadrant = popular switch
                {
                    true when profitable => "Estrella",
                    true => "Vaca",
                    false when profitable => "Rompecabezas",
                    _ => "Perro",
                };
                return new MenuItemAggregate(
                    x.ProductId,
                    x.ProductName,
                    x.ProductTypeName,
                    x.Quantity,
                    x.Revenue,
                    x.Margin,
                    x.MarginPercent,
                    mixPct,
                    quadrant);
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var quadrantCounts = classified
            .GroupBy(x => x.Quadrant!)
            .ToDictionary(g => g.Key, g => g.Count());

        return new StrategicJsonReportDocument
        {
            Meta = BuildMeta(
                StrategicReportTypes.MenuEngineering,
                "Ingeniería de menú",
                "Matriz popularidad vs rentabilidad (costo capturado al momento del pago; ventas antiguas usan costo actual).",
                startDate,
                endDate,
                null),
            Summary = new StrategicJsonReportSummary
            {
                Kpis =
                [
                    Kpi("products", "Productos vendidos", classified.Count.ToString(CultureInfo.InvariantCulture)),
                    Kpi("revenue", "Ingresos", FormatMoney(totalRevenue)),
                    Kpi("median_margin", "Margen mediano", $"{medianMarginPct:N1}%"),
                    Kpi("stars", "Estrellas", quadrantCounts.GetValueOrDefault("Estrella").ToString(CultureInfo.InvariantCulture), tone: "success"),
                    Kpi("dogs", "Perros", quadrantCounts.GetValueOrDefault("Perro").ToString(CultureInfo.InvariantCulture), tone: "warning"),
                ],
            },
            Sections =
            [
                MatrixSection(
                    "Matriz de menú",
                    "Mix % (popularidad)",
                    "Margen % (rentabilidad)",
                    medianQty / Math.Max(totalQty, 1m) * 100m,
                    medianMarginPct,
                    classified.Select(x => new StrategicJsonMatrixItem
                    {
                        Label = x.ProductName,
                        Quadrant = x.Quadrant!,
                        X = x.MixPercent,
                        Y = x.MarginPercent,
                        Detail = $"{x.ProductTypeName} · {FormatMoney(x.Revenue)}",
                    }).ToList()),
                TableSection(
                    "Detalle por producto",
                    [
                        Col("product", "Producto"),
                        Col("category", "Categoría"),
                        Col("quadrant", "Cuadrante"),
                        Col("quantity", "Cantidad", "right", "number"),
                        Col("mixPercent", "Mix %", "right", "percent"),
                        Col("revenue", "Ingresos", "right", "money"),
                        Col("margin", "Margen", "right", "money"),
                        Col("marginPercent", "Margen %", "right", "percent"),
                    ],
                    classified.Select(x => Row(
                        ("product", x.ProductName),
                        ("category", x.ProductTypeName),
                        ("quadrant", x.Quadrant!),
                        ("quantity", x.Quantity),
                        ("mixPercent", x.MixPercent),
                        ("revenue", x.Revenue),
                        ("margin", x.Margin),
                        ("marginPercent", x.MarginPercent))).ToList()),
                ChartSection(
                    "Top ingresos",
                    "bar",
                    classified.Take(10).Select(x => new StrategicJsonChartPoint
                    {
                        Category = x.ProductName,
                        Value = x.Revenue,
                        Detail = x.Quadrant,
                    }).ToList(),
                    "Ingresos"),
            ],
        };
    }

    private async Task<StrategicJsonReportDocument> BuildSalesForecastAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        int forecastDays,
        CancellationToken cancellationToken)
    {
        var lines = await LoadPaidSalesLinesAsync(tenantId, startDate, endDate, cancellationToken);

        var daily = lines
            .GroupBy(l => DateOnly.FromDateTime(l.CreatedAtUtc))
            .Select(g => new DailyBucket(g.Key, g.Sum(x => x.LineTotal), g.Sum(x => x.Quantity), g.Select(x => x.SalesOrderId).Distinct().Count()))
            .OrderBy(x => x.Date)
            .ToList();

        var weekdayAvg = daily
            .GroupBy(d => d.Date.DayOfWeek)
            .ToDictionary(
                g => g.Key,
                g => new DailyBucket(
                    default,
                    g.Average(x => x.Revenue),
                    g.Average(x => x.Quantity),
                    (int)Math.Round(g.Average(x => (decimal)x.OrderCount))));

        var forecastStart = endDate.AddDays(1);
        var forecast = Enumerable.Range(0, forecastDays)
            .Select(offset =>
            {
                var date = forecastStart.AddDays(offset);
                var avg = weekdayAvg.GetValueOrDefault(date.DayOfWeek);
                return new DailyBucket(
                    date,
                    avg?.Revenue ?? 0m,
                    avg?.Quantity ?? 0m,
                    (int)Math.Round(avg?.OrderCount ?? 0m));
            })
            .ToList();

        var historicalPoints = daily.Select(d => new StrategicJsonChartPoint
        {
            Category = d.Date.ToString("yyyy-MM-dd"),
            Value = d.Revenue,
            SeriesKey = "historical",
            Detail = $"{d.Quantity:N0} ítems",
        }).ToList();

        var forecastPoints = forecast.Select(d => new StrategicJsonChartPoint
        {
            Category = d.Date.ToString("yyyy-MM-dd"),
            Value = d.Revenue,
            SeriesKey = "forecast",
            Detail = $"{d.Quantity:N0} ítems (est.)",
        }).ToList();

        var avgDailyRevenue = daily.Count > 0 ? daily.Average(d => d.Revenue) : 0m;
        var projectedRevenue = forecast.Sum(f => f.Revenue);

        return new StrategicJsonReportDocument
        {
            Meta = BuildMeta(
                StrategicReportTypes.SalesForecast,
                "Pronóstico de ventas",
                "Proyección por promedio del mismo día de la semana en el período histórico.",
                startDate,
                endDate,
                forecastDays),
            Summary = new StrategicJsonReportSummary
            {
                Kpis =
                [
                    Kpi("days", "Días históricos", daily.Count.ToString(CultureInfo.InvariantCulture)),
                    Kpi("avg_daily", "Promedio diario", FormatMoney(avgDailyRevenue)),
                    Kpi("forecast_total", $"Proyección {forecastDays}d", FormatMoney(projectedRevenue)),
                    Kpi("forecast_days", "Horizonte", $"{forecastDays} días"),
                ],
            },
            Sections =
            [
                TextSection(
                    "Metodología",
                    "Se agrupan las ventas pagadas por día calendario. Para cada día futuro se usa el promedio de ingresos de ese mismo día de la semana dentro del rango seleccionado."),
                MultiSeriesChartSection(
                    "Histórico vs pronóstico (ingresos)",
                    "line",
                    [
                        new StrategicJsonChartSeries { Key = "historical", Label = "Histórico", Color = "#228be6" },
                        new StrategicJsonChartSeries { Key = "forecast", Label = "Pronóstico", Color = "#fab005" },
                    ],
                    [.. historicalPoints, .. forecastPoints],
                    "Ingresos"),
                TableSection(
                    "Pronóstico diario",
                    [
                        Col("date", "Fecha"),
                        Col("revenue", "Ingresos est.", "right", "money"),
                        Col("quantity", "Ítems est.", "right", "number"),
                        Col("orders", "Pedidos est.", "right", "number"),
                    ],
                    forecast.Select(f => Row(
                        ("date", f.Date.ToString("yyyy-MM-dd")),
                        ("revenue", f.Revenue),
                        ("quantity", f.Quantity),
                        ("orders", f.OrderCount))).ToList()),
            ],
        };
    }

    private async Task<StrategicJsonReportDocument> BuildIngredientForecastAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        int forecastDays,
        CancellationToken cancellationToken)
    {
        var lines = await LoadPaidSalesLinesAsync(tenantId, startDate, endDate, cancellationToken);
        var spanDays = Math.Max(1, endDate.DayNumber - startDate.DayNumber + 1);

        var specs = lines.Select(l => (l.ProductId, l.Quantity, l.ExcludedIngredientIds)).ToList();
        var totalDemand = new Dictionary<Guid, decimal>();
        await ProductInventoryExpansion.AddIngredientDemandAsync(_db, totalDemand, specs, cancellationToken);

        var ingredientIds = totalDemand.Keys.ToList();
        var ingredients = await _db.Ingredients
            .AsNoTracking()
            .Where(i => ingredientIds.Contains(i.Id))
            .Select(i => new
            {
                i.Id,
                i.Name,
                Unit = i.Unit.ToString(),
                i.StockQuantity,
                i.ReorderLevel,
                i.UnitCost,
            })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var rows = totalDemand
            .Select(kvp =>
            {
                ingredients.TryGetValue(kvp.Key, out var ing);
                var dailyAvg = kvp.Value / spanDays;
                var projected = dailyAvg * forecastDays;
                var stock = ing?.StockQuantity ?? 0m;
                var daysCover = dailyAvg > 0 ? stock / dailyAvg : (decimal?)null;
                var runOutDate = daysCover.HasValue
                    ? DateOnly.FromDateTime(DateTime.UtcNow).AddDays((int)Math.Floor(daysCover.Value))
                    : (DateOnly?)null;
                var belowReorder = ing?.ReorderLevel is not null && stock <= ing.ReorderLevel;
                return new IngredientForecastRow(
                    ing?.Name ?? kvp.Key.ToString(),
                    ing?.Unit ?? "",
                    kvp.Value,
                    dailyAvg,
                    projected,
                    stock,
                    ing?.ReorderLevel,
                    daysCover,
                    runOutDate,
                    belowReorder);
            })
            .OrderBy(r => r.DaysOfCover ?? decimal.MaxValue)
            .ThenByDescending(r => r.ProjectedUsage)
            .ToList();

        var critical = rows.Count(r => r.DaysOfCover is < 7);
        var belowReorderCount = rows.Count(r => r.BelowReorder);

        return new StrategicJsonReportDocument
        {
            Meta = BuildMeta(
                StrategicReportTypes.IngredientForecast,
                "Pronóstico de ingredientes",
                "Consumo teórico según ventas × receta; días de cobertura con stock actual.",
                startDate,
                endDate,
                forecastDays),
            Summary = new StrategicJsonReportSummary
            {
                Kpis =
                [
                    Kpi("ingredients", "Ingredientes usados", rows.Count.ToString(CultureInfo.InvariantCulture)),
                    Kpi("critical", "< 7 días stock", critical.ToString(CultureInfo.InvariantCulture), tone: critical > 0 ? "danger" : "success"),
                    Kpi("reorder", "Bajo reorden", belowReorderCount.ToString(CultureInfo.InvariantCulture), tone: belowReorderCount > 0 ? "warning" : "default"),
                    Kpi("horizon", "Horizonte", $"{forecastDays} días"),
                ],
            },
            Sections =
            [
                TextSection(
                    "Metodología",
                    "Se expande cada línea de venta pagada a demanda de ingredientes (incluye combos y exclusiones). El consumo diario promedio = total del período ÷ días del rango."),
                TableSection(
                    "Proyección por ingrediente",
                    [
                        Col("ingredient", "Ingrediente"),
                        Col("unit", "Unidad"),
                        Col("periodUsage", "Uso período", "right", "number"),
                        Col("dailyAvg", "Prom. diario", "right", "number"),
                        Col("projected", $"Uso {forecastDays}d", "right", "number"),
                        Col("stock", "Stock", "right", "number"),
                        Col("daysCover", "Días cobertura", "right", "number"),
                        Col("runOut", "Agotamiento est.", "left", "date"),
                    ],
                    rows.Select(r => Row(
                        ("ingredient", r.Name),
                        ("unit", r.Unit),
                        ("periodUsage", r.PeriodUsage),
                        ("dailyAvg", r.DailyAverage),
                        ("projected", r.ProjectedUsage),
                        ("stock", r.Stock),
                        ("daysCover", r.DaysOfCover),
                        ("runOut", r.RunOutDate?.ToString("yyyy-MM-dd")))).ToList()),
                ChartSection(
                    "Menor cobertura (días)",
                    "bar",
                    rows
                        .Where(r => r.DaysOfCover.HasValue)
                        .Take(10)
                        .Select(r => new StrategicJsonChartPoint
                        {
                            Category = r.Name,
                            Value = r.DaysOfCover!.Value,
                            Detail = $"{r.Stock:N2} {r.Unit}",
                        }).ToList(),
                    "Días"),
            ],
        };
    }

    private async Task<StrategicJsonReportDocument> BuildFoodCostMarginAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var lines = await LoadPaidSalesLinesAsync(tenantId, startDate, endDate, cancellationToken);
        var fallbackCosts = await LoadFallbackUnitCostsAsync(lines, cancellationToken);
        var linesWithSnapshot = lines.Count(l => l.UnitCostPrice.HasValue);
        var usesLegacyFallback = linesWithSnapshot < lines.Count;

        var byProduct = lines
            .GroupBy(l => l.ProductId)
            .Select(g =>
            {
                var first = g.First();
                var qty = g.Sum(x => x.Quantity);
                var revenue = g.Sum(x => x.LineTotal);
                var cogs = g.Sum(x => LineCogs(x, fallbackCosts));
                var margin = revenue - cogs;
                var foodCostPct = revenue > 0 ? cogs / revenue * 100m : 0m;
                return new FoodCostRow(first.ProductName, qty, revenue, cogs, margin, foodCostPct);
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var totalRevenue = byProduct.Sum(x => x.Revenue);
        var totalCogs = byProduct.Sum(x => x.Cogs);
        var totalMargin = totalRevenue - totalCogs;
        var foodCostPercent = totalRevenue > 0 ? totalCogs / totalRevenue * 100m : 0m;

        return new StrategicJsonReportDocument
        {
            Meta = BuildMeta(
                StrategicReportTypes.FoodCostMargin,
                "Food cost y margen",
                "COGS con costo capturado al pagar cada línea de venta.",
                startDate,
                endDate,
                null),
            Summary = new StrategicJsonReportSummary
            {
                Kpis =
                [
                    Kpi("revenue", "Ingresos", FormatMoney(totalRevenue)),
                    Kpi("cogs", "COGS", FormatMoney(totalCogs)),
                    Kpi("margin", "Margen bruto", FormatMoney(totalMargin), tone: totalMargin >= 0 ? "success" : "danger"),
                    Kpi("food_cost", "Food cost %", $"{foodCostPercent:N1}%", tone: foodCostPercent > 35 ? "warning" : "default"),
                ],
            },
            Sections =
            [
                usesLegacyFallback
                    ? AlertSection(
                        "Nota",
                        "Algunas ventas del período no tienen costo guardado al pagar; esas líneas usan el costo de receta actual como aproximación.",
                        "blue")
                    : AlertSection(
                        "Nota",
                        "COGS calculado con el costo unitario guardado al momento del pago de cada línea.",
                        "teal"),
                TableSection(
                    "Margen por producto",
                    [
                        Col("product", "Producto"),
                        Col("quantity", "Cantidad", "right", "number"),
                        Col("revenue", "Ingresos", "right", "money"),
                        Col("cogs", "COGS", "right", "money"),
                        Col("margin", "Margen", "right", "money"),
                        Col("foodCostPercent", "Food cost %", "right", "percent"),
                    ],
                    byProduct.Select(r => Row(
                        ("product", r.ProductName),
                        ("quantity", r.Quantity),
                        ("revenue", r.Revenue),
                        ("cogs", r.Cogs),
                        ("margin", r.Margin),
                        ("foodCostPercent", r.FoodCostPercent))).ToList()),
                ChartSection(
                    "Food cost % por producto (top 10 ingresos)",
                    "bar",
                    byProduct.Take(10).Select(r => new StrategicJsonChartPoint
                    {
                        Category = r.ProductName,
                        Value = r.FoodCostPercent,
                        Detail = FormatMoney(r.Revenue),
                    }).ToList(),
                    "Food cost %"),
            ],
        };
    }

    private async Task<StrategicJsonReportDocument> BuildSupplierAbcAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var purchaseLines = await _db.PurchaseLines
            .AsNoTracking()
            .Where(pl =>
                pl.TenantId == tenantId
                && pl.Purchase.PurchasedAtUtc >= startUtc
                && pl.Purchase.PurchasedAtUtc < endExclusive)
            .Select(pl => new
            {
                pl.LineTotal,
                ProviderName = pl.Purchase.Provider.Name,
                ProviderId = pl.Purchase.ProviderId,
            })
            .ToListAsync(cancellationToken);

        var grouped = purchaseLines
            .GroupBy(x => x.ProviderId)
            .Select(g => new SupplierSpendRow(g.First().ProviderName, g.Sum(x => x.LineTotal)))
            .OrderByDescending(x => x.Spend)
            .ToList();

        var grandTotal = grouped.Sum(x => x.Spend);
        decimal cumulative = 0m;
        var classified = grouped.Select(row =>
        {
            cumulative += row.Spend;
            var share = grandTotal > 0 ? row.Spend / grandTotal * 100m : 0m;
            var cumulativePct = grandTotal > 0 ? cumulative / grandTotal * 100m : 0m;
            var band = cumulativePct <= 80m ? "A" : cumulativePct <= 95m ? "B" : "C";
            return row with { SharePercent = share, CumulativePercent = cumulativePct, AbcBand = band };
        }).ToList();

        return new StrategicJsonReportDocument
        {
            Meta = BuildMeta(
                StrategicReportTypes.SupplierAbc,
                "ABC de proveedores",
                "Concentración de gasto en compras por proveedor (regla 80/15/5).",
                startDate,
                endDate,
                null),
            Summary = new StrategicJsonReportSummary
            {
                Kpis =
                [
                    Kpi("providers", "Proveedores", classified.Count.ToString(CultureInfo.InvariantCulture)),
                    Kpi("spend", "Gasto total", FormatMoney(grandTotal)),
                    Kpi("a_band", "Clase A", classified.Count(x => x.AbcBand == "A").ToString(CultureInfo.InvariantCulture)),
                    Kpi("top_share", "Top proveedor", classified.Count > 0 ? $"{classified[0].SharePercent:N1}%" : "—"),
                ],
            },
            Sections =
            [
                TableSection(
                    "Ranking de proveedores",
                    [
                        Col("provider", "Proveedor"),
                        Col("spend", "Gasto", "right", "money"),
                        Col("share", "Participación %", "right", "percent"),
                        Col("cumulative", "Acumulado %", "right", "percent"),
                        Col("band", "Clase ABC"),
                    ],
                    classified.Select(r => Row(
                        ("provider", r.ProviderName),
                        ("spend", r.Spend),
                        ("share", r.SharePercent),
                        ("cumulative", r.CumulativePercent),
                        ("band", r.AbcBand))).ToList()),
                ChartSection(
                    "Gasto por proveedor",
                    "bar",
                    classified.Take(10).Select(r => new StrategicJsonChartPoint
                    {
                        Category = r.ProviderName,
                        Value = r.Spend,
                        Detail = $"Clase {r.AbcBand}",
                    }).ToList(),
                    "Gasto"),
            ],
        };
    }

    private async Task<StrategicJsonReportDocument> BuildProductMixByHourAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var lines = await LoadPaidSalesLinesAsync(tenantId, startDate, endDate, cancellationToken);

        var hourly = Enumerable.Range(0, 24)
            .Select(h => new HourBucket(h, 0m, 0m))
            .ToDictionary(x => x.Hour);

        foreach (var line in lines)
        {
            var hour = line.CreatedAtUtc.Hour;
            var bucket = hourly[hour];
            hourly[hour] = bucket with
            {
                Revenue = bucket.Revenue + line.LineTotal,
                Quantity = bucket.Quantity + line.Quantity,
            };
        }

        var peakHour = hourly.Values.OrderByDescending(x => x.Revenue).First();
        var totalRevenue = hourly.Values.Sum(x => x.Revenue);

        var topProductsByHour = lines
            .GroupBy(l => (l.CreatedAtUtc.Hour, l.ProductName))
            .Select(g => new { g.Key.Item1, g.Key.ProductName, Revenue = g.Sum(x => x.LineTotal), Qty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Revenue)
            .Take(30)
            .ToList();

        return new StrategicJsonReportDocument
        {
            Meta = BuildMeta(
                StrategicReportTypes.ProductMixByHour,
                "Mix de productos por hora",
                "Distribución de ventas por hora del día (UTC) y productos destacados.",
                startDate,
                endDate,
                null),
            Summary = new StrategicJsonReportSummary
            {
                Kpis =
                [
                    Kpi("peak_hour", "Hora pico", $"{peakHour.Hour:00}:00 UTC"),
                    Kpi("peak_revenue", "Ingresos hora pico", FormatMoney(peakHour.Revenue)),
                    Kpi("total", "Ingresos período", FormatMoney(totalRevenue)),
                    Kpi("lines", "Líneas vendidas", lines.Count.ToString(CultureInfo.InvariantCulture)),
                ],
            },
            Sections =
            [
                TextSection(
                    "Zona horaria",
                    "Las horas se calculan en UTC según CreatedAtUtc de cada línea de venta."),
                ChartSection(
                    "Ingresos por hora",
                    "bar",
                    hourly.Values.Select(h => new StrategicJsonChartPoint
                    {
                        Category = $"{h.Hour:00}:00",
                        Value = h.Revenue,
                        Detail = $"{h.Quantity:N0} ítems",
                    }).ToList(),
                    "Ingresos"),
                TableSection(
                    "Top productos por hora",
                    [
                        Col("hour", "Hora", "center"),
                        Col("product", "Producto"),
                        Col("quantity", "Cantidad", "right", "number"),
                        Col("revenue", "Ingresos", "right", "money"),
                    ],
                    topProductsByHour.Select(r => Row(
                        ("hour", $"{r.Item1:00}:00"),
                        ("product", r.ProductName),
                        ("quantity", r.Qty),
                        ("revenue", r.Revenue))).ToList()),
            ],
        };
    }

    private static StrategicJsonReportMeta BuildMeta(
        string reportType,
        string title,
        string description,
        DateOnly startDate,
        DateOnly endDate,
        int? forecastDays) =>
        new()
        {
            ReportType = reportType,
            Title = title,
            Description = description,
            StartDate = startDate,
            EndDate = endDate,
            ForecastDays = forecastDays,
            GeneratedAtUtc = DateTime.UtcNow,
            FromCache = false,
        };

    private static StrategicJsonKpi Kpi(string id, string label, string value, string? hint = null, string tone = "default") =>
        new() { Id = id, Label = label, Value = value, Hint = hint, Tone = tone };

    private static string FormatMoney(decimal value) =>
        value.ToString("C0", new CultureInfo("es-CO"));

    private static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        if (sorted.Count == 0)
            return 0m;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2m
            : sorted[mid];
    }

    private static StrategicJsonTableColumn Col(string key, string label, string align = "left", string? format = null) =>
        new() { Key = key, Label = label, Align = align, Format = format };

    private static Dictionary<string, object?> Row(params (string key, object? value)[] cells) =>
        cells.ToDictionary(c => c.key, c => c.value);

    private static StrategicJsonReportSection TextSection(string title, string content) =>
        new() { Type = "text", Title = title, Content = content };

    private static StrategicJsonReportSection AlertSection(string title, string content, string tone) =>
        new() { Type = "alert", Title = title, Content = content, AlertTone = tone };

    private static StrategicJsonReportSection TableSection(
        string title,
        IReadOnlyList<StrategicJsonTableColumn> columns,
        IReadOnlyList<Dictionary<string, object?>> rows) =>
        new() { Type = "table", Title = title, Columns = columns, Rows = rows };

    private static StrategicJsonReportSection ChartSection(
        string title,
        string chartType,
        IReadOnlyList<StrategicJsonChartPoint> points,
        string valueLabel) =>
        new() { Type = "chart", Title = title, ChartType = chartType, Points = points, ValueLabel = valueLabel };

    private static StrategicJsonReportSection MultiSeriesChartSection(
        string title,
        string chartType,
        IReadOnlyList<StrategicJsonChartSeries> series,
        IReadOnlyList<StrategicJsonChartPoint> points,
        string valueLabel) =>
        new() { Type = "chart", Title = title, ChartType = chartType, Series = series, Points = points, ValueLabel = valueLabel };

    private static StrategicJsonReportSection MatrixSection(
        string title,
        string xLabel,
        string yLabel,
        decimal xThreshold,
        decimal yThreshold,
        IReadOnlyList<StrategicJsonMatrixItem> items) =>
        new()
        {
            Type = "matrix",
            Title = title,
            XAxisLabel = xLabel,
            YAxisLabel = yLabel,
            XThreshold = xThreshold,
            YThreshold = yThreshold,
            Items = items,
        };

    private async Task<Dictionary<Guid, decimal>> LoadFallbackUnitCostsAsync(
        IReadOnlyList<SalesLineRow> lines,
        CancellationToken cancellationToken)
    {
        var productIds = lines
            .Where(l => !l.UnitCostPrice.HasValue)
            .Select(l => l.ProductId)
            .Distinct()
            .ToList();

        if (productIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        return await ProductCostCalculator.GetCostPricesByProductIdsAsync(_db, productIds, cancellationToken);
    }

    private static decimal LineCogs(SalesLineRow line, IReadOnlyDictionary<Guid, decimal> fallbackUnitCostByProduct)
    {
        var unitCost = line.UnitCostPrice ?? fallbackUnitCostByProduct.GetValueOrDefault(line.ProductId);
        return unitCost * line.Quantity;
    }

    private sealed record SalesLineRow(
        Guid ProductId,
        string ProductName,
        string ProductTypeName,
        decimal Quantity,
        decimal LineTotal,
        decimal? UnitCostPrice,
        Guid SalesOrderId,
        DateTime CreatedAtUtc,
        Guid LineId)
    {
        public HashSet<Guid> ExcludedIngredientIds { get; init; } = [];
    }

    private sealed record MenuItemAggregate(
        Guid ProductId,
        string ProductName,
        string ProductTypeName,
        decimal Quantity,
        decimal Revenue,
        decimal Margin,
        decimal MarginPercent,
        decimal MixPercent = 0m,
        string? Quadrant = null);

    private sealed record DailyBucket(DateOnly Date, decimal Revenue, decimal Quantity, int OrderCount);

    private sealed record IngredientForecastRow(
        string Name,
        string Unit,
        decimal PeriodUsage,
        decimal DailyAverage,
        decimal ProjectedUsage,
        decimal Stock,
        decimal? ReorderLevel,
        decimal? DaysOfCover,
        DateOnly? RunOutDate,
        bool BelowReorder);

    private sealed record FoodCostRow(
        string ProductName,
        decimal Quantity,
        decimal Revenue,
        decimal Cogs,
        decimal Margin,
        decimal FoodCostPercent);

    private sealed record SupplierSpendRow(string ProviderName, decimal Spend)
    {
        public decimal SharePercent { get; init; }
        public decimal CumulativePercent { get; init; }
        public string AbcBand { get; init; } = "C";
    }

    private sealed record HourBucket(int Hour, decimal Revenue, decimal Quantity);
}
