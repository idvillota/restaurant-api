using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Reports;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.ReportsStrategicAi)]
[Route("api/strategic-reports")]
public sealed class StrategicReportsController : ControllerBase
{
    private readonly IStrategicAiReportService _legacyService;
    private readonly IStrategicAnalyticsService _analyticsService;

    public StrategicReportsController(
        IStrategicAiReportService legacyService,
        IStrategicAnalyticsService analyticsService)
    {
        _legacyService = legacyService;
        _analyticsService = analyticsService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(StrategicReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StrategicReportDto>> GetLegacy(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _legacyService.GetStrategicReportAsync(startDate, endDate, refresh, cancellationToken);
            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("types")]
    [ProducesResponseType(typeof(IReadOnlyList<StrategicReportTypeInfo>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<StrategicReportTypeInfo>> ListTypes() =>
        Ok(StrategicReportCatalog.All);

    [HttpGet("{reportType}")]
    [ProducesResponseType(typeof(StrategicJsonReportDocument), StatusCodes.Status200OK)]
    public async Task<ActionResult<StrategicJsonReportDocument>> GetAnalytics(
        string reportType,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] int? forecastDays = null,
        [FromQuery] bool refresh = false,
        [FromQuery] bool includeAiInsights = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _analyticsService.GetReportAsync(
                reportType,
                startDate,
                endDate,
                forecastDays,
                refresh,
                includeAiInsights,
                cancellationToken);
            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed class StrategicReportTypeInfo
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public bool SupportsForecastDays { get; init; }
}

internal static class StrategicReportCatalog
{
    public static IReadOnlyList<StrategicReportTypeInfo> All { get; } =
    [
        new()
        {
            Id = StrategicReportTypes.MenuEngineering,
            Title = "Ingeniería de menú",
            Description = "Matriz popularidad vs rentabilidad por producto.",
        },
        new()
        {
            Id = StrategicReportTypes.SalesForecast,
            Title = "Pronóstico de ventas",
            Description = "Proyección de ingresos por día de la semana.",
            SupportsForecastDays = true,
        },
        new()
        {
            Id = StrategicReportTypes.IngredientForecast,
            Title = "Pronóstico de ingredientes",
            Description = "Días de cobertura y agotamiento estimado.",
            SupportsForecastDays = true,
        },
        new()
        {
            Id = StrategicReportTypes.FoodCostMargin,
            Title = "Food cost y margen",
            Description = "COGS teórico vs ingresos por producto.",
        },
        new()
        {
            Id = StrategicReportTypes.SupplierAbc,
            Title = "ABC de proveedores",
            Description = "Concentración de gasto en compras.",
        },
        new()
        {
            Id = StrategicReportTypes.ProductMixByHour,
            Title = "Mix por hora",
            Description = "Distribución de ventas por hora del día.",
        },
    ];
}
