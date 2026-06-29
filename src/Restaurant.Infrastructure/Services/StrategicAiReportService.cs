using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
// Regex removed: HTML cleaning removed, responses are plain text
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.Reports;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class StrategicAiReportService : IStrategicAiReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenant;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureOpenAiOptions _azure;
    private readonly ILogger<StrategicAiReportService> _logger;

    public StrategicAiReportService(
        ApplicationDbContext db,
        ICurrentTenantContext tenant,
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiOptions> azure,
        ILogger<StrategicAiReportService> logger)
    {
        _db = db;
        _tenant = tenant;
        _httpClientFactory = httpClientFactory;
        _azure = azure.Value;
        _logger = logger;
    }

    public async Task<StrategicReportDto> GetStrategicReportAsync(
        DateOnly salesStartDate,
        DateOnly salesEndDate,
        bool refresh,
        CancellationToken cancellationToken = default)
    {
        if (salesEndDate < salesStartDate)
            throw new InvalidOperationException("La fecha final debe ser igual o posterior a la fecha inicial.");

        var spanDays = salesEndDate.DayNumber - salesStartDate.DayNumber;
        if (spanDays > 366)
            throw new InvalidOperationException("El rango de ventas no puede superar 366 días.");

        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No se pudo determinar el local activo.");

        var cacheDate = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!refresh)
        {
            var cached = await _db.StrategicAiReportCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c =>
                        c.TenantId == tenantId
                        && c.SalesStartDate == salesStartDate
                        && c.SalesEndDate == salesEndDate
                        && c.CacheDate == cacheDate,
                    cancellationToken);

            if (cached is not null)
            {
                return new StrategicReportDto
                {
                    Html = cached.HtmlContent,
                    FromCache = true,
                    GeneratedAtUtc = cached.GeneratedAtUtc,
                    SalesStartDate = salesStartDate,
                    SalesEndDate = salesEndDate,
                };
            }
        }

        var inventoryContext = await BuildInventoryContextAsync(tenantId, cancellationToken);
        var salesContext = await BuildSalesContextAsync(tenantId, salesStartDate, salesEndDate, cancellationToken);

        var html = await GenerateHtmlAsync(
            inventoryContext,
            salesContext,
            salesStartDate,
            salesEndDate,
            cancellationToken);

        var generatedAtUtc = DateTime.UtcNow;
        await UpsertCacheAsync(
            tenantId,
            salesStartDate,
            salesEndDate,
            cacheDate,
            html,
            generatedAtUtc,
            cancellationToken);

        return new StrategicReportDto
        {
            Html = html,
            FromCache = false,
            GeneratedAtUtc = generatedAtUtc,
            SalesStartDate = salesStartDate,
            SalesEndDate = salesEndDate,
        };
    }

    private async Task<string> BuildInventoryContextAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new
            {
                i.Id,
                i.Name,
                Unit = i.Unit.ToString(),
                i.UnitCost,
                i.StockQuantity,
                i.ReorderLevel,
                Category = i.IngredientCategory.Name,
            })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Id\tNombre\tCategoría\tUnidad\tCostoUnitario\tStock\tNivelReorden");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(
                '\t',
                row.Id,
                row.Name,
                row.Category,
                row.Unit,
                row.UnitCost?.ToString() ?? "NULL",
                row.StockQuantity?.ToString() ?? "NULL",
                row.ReorderLevel?.ToString() ?? "NULL"));
        }

        return sb.ToString();
    }

    private async Task<string> BuildSalesContextAsync(
        Guid tenantId,
        DateOnly salesStartDate,
        DateOnly salesEndDate,
        CancellationToken cancellationToken)
    {
        var startUtc = salesStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = salesEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Single DB round-trip; aggregate product/daily/hourly buckets in memory.
        var paidLineRows = await _db.SalesOrderLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId
                && l.CreatedAtUtc >= startUtc
                && l.CreatedAtUtc < endExclusive
                && l.SalesOrder.Status == SalesOrderStatus.Paid)
            .Select(l => new PaidSalesLineRow(
                l.Product.Name,
                l.Quantity,
                l.LineTotal,
                l.SalesOrderId,
                l.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var productTotals = paidLineRows
            .GroupBy(l => l.ProductName)
            .Select(g => new
            {
                ProductName = g.Key,
                Quantity = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.LineTotal),
                OrderCount = g.Select(l => l.SalesOrderId).Distinct().Count(),
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var dailyTotals = paidLineRows
            .GroupBy(l => DateOnly.FromDateTime(l.CreatedAtUtc))
            .Select(g => new
            {
                Date = g.Key,
                Quantity = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.LineTotal),
                OrderCount = g.Select(l => l.SalesOrderId).Distinct().Count(),
            })
            .OrderBy(x => x.Date)
            .ToList();

        var hourlyTotals = paidLineRows
            .GroupBy(l => l.CreatedAtUtc.Hour)
            .Select(g => new
            {
                Hour = g.Key,
                Quantity = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.LineTotal),
            })
            .OrderBy(x => x.Hour)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("=== RESUMEN POR PRODUCTO (ventas pagadas) ===");
        sb.AppendLine("Producto\tCantidad\tIngresoBruto\tPedidos");
        foreach (var row in productTotals)
        {
            sb.AppendLine(string.Join(
                '\t',
                row.ProductName,
                row.Quantity.ToString(),
                row.Revenue.ToString(),
                row.OrderCount.ToString()));
        }

        sb.AppendLine();
        sb.AppendLine("=== RESUMEN POR DÍA ===");
        sb.AppendLine("Fecha\tCantidad\tIngresoBruto\tPedidos");
        foreach (var row in dailyTotals)
        {
            sb.AppendLine(string.Join(
                '\t',
                row.Date.ToString("yyyy-MM-dd"),
                row.Quantity.ToString(),
                row.Revenue.ToString(),
                row.OrderCount.ToString()));
        }

        sb.AppendLine();
        sb.AppendLine("=== DISTRIBUCIÓN POR HORA (UTC) ===");
        sb.AppendLine("Hora\tCantidad\tIngresoBruto");
        foreach (var row in hourlyTotals)
        {
            sb.AppendLine(string.Join(
                '\t',
                row.Hour.ToString("00"),
                row.Quantity.ToString(),
                row.Revenue.ToString()));
        }

        return sb.ToString();
    }

    private async Task<string> GenerateHtmlAsync(
        string inventoryContext,
        string salesContext,
        DateOnly salesStartDate,
        DateOnly salesEndDate,
        CancellationToken cancellationToken)
    {
        // Use Azure OpenAI via named HttpClient 'AzureOpenAiClient'.
        var azureEndpoint = _azure.Endpoint?.Trim();
        var prompt = BuildMasterPrompt(inventoryContext, salesContext, salesStartDate, salesEndDate);

        if (string.IsNullOrWhiteSpace(azureEndpoint) || string.IsNullOrWhiteSpace(_azure.DeploymentName))
            throw new InvalidOperationException("Azure OpenAI no está configurado. Defina AzureOpenAi:Endpoint y AzureOpenAi:DeploymentName en la configuración.");

        if (prompt.Length > _azure.MaxPromptChars)
        {
            _logger.LogWarning("Prompt demasiado largo ({Length} chars). Se aplicará truncado seguro.", prompt.Length);
            var maxPerContext = Math.Max(16_000, _azure.MaxPromptChars / 3);
            inventoryContext = inventoryContext.Length > maxPerContext
                ? inventoryContext.Substring(0, maxPerContext)
                : inventoryContext;
            salesContext = salesContext.Length > maxPerContext
                ? salesContext.Substring(0, maxPerContext)
                : salesContext;
            prompt = BuildMasterPrompt(inventoryContext, salesContext, salesStartDate, salesEndDate);
        }

        var deployment = _azure.DeploymentName.Trim();
        var payload = new
        {
            messages = new[]
            {
                new { role = "system", content = "Actúa como un analista de datos. Responde únicamente con TEXTO PLANO en español. No uses HTML ni Markdown." },
                new { role = "user", content = prompt }
            },
            max_tokens = 8000,
            temperature = 0.2f
        };

        var client = _httpClientFactory.CreateClient("AzureOpenAiClient");
        var azureUrl = $"/openai/deployments/{deployment}/chat/completions?api-version=2023-05-15";

        using var request = new HttpRequestMessage(HttpMethod.Post, azureUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Azure OpenAI error {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException("El servicio de IA (Azure OpenAI) rechazó la solicitud. Revise la configuración y el modelo configurado.");
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return content.Trim();
    }



    private static string BuildMasterPrompt(
        string inventoryContext,
        string salesContext,
        DateOnly salesStartDate,
        DateOnly salesEndDate)
    {
        return $"""
            Actúa como un Desarrollador Frontend Senior experto en UI/UX y Dashboards Corporativos de Alta Gama. Tu objetivo es generar un único archivo autónomo de PURE HTML y CSS integrado en una etiqueta <style> basado estrictamente en los datos del restaurante que te proveeré abajo.

            REQUISITOS DE DISEÑO INTERFAZ V3.5:
            - Alta gama, profesional, ultra limpio, responsivo y moderno.
            - Paleta de colores ejecutiva V3.5: Fondo de página #f8f9fa. Acentos en verde esmeralda (#198754), ámbar (#ffc107) para advertencias y rojo carmesí (#dc3545) para alertas críticas.
            - Fuentes tipográficas del sistema claras (Segoe UI, system-ui). No utilices formato Markdown en tu respuesta, responde ÚNICAMENTE con el código estructurado HTML/CSS válido.
            - Todo el contenido visible del informe debe estar en español.

            PERÍODO DE ANÁLISIS DE VENTAS (inclusive):
            - Fecha inicio: {salesStartDate:yyyy-MM-dd}
            - Fecha fin: {salesEndDate:yyyy-MM-dd}
            Usa exclusivamente las líneas de ventas dentro de este rango para métricas, tablas y conclusiones de ventas.

            DATOS EN VIVO DEL SISTEMA:
            --- INVENTARIO ACTUAL ---
            {inventoryContext}

            --- HISTORIAL DE VENTAS (RANGO INDICADO) ---
            {salesContext}

            ESTRUCTURA OBLIGATORIA DEL DOCUMENTO (Nivel de Auditoría V3.5):
            1. ENCABEZADO EJECUTIVO (MEMORANDO V3.5) 
                - Título Principal destacado: "REPORTE DE INTELIGENCIA OPERATIVA V3.5 - {salesStartDate:yyyy-MM-dd} a {salesEndDate:yyyy-MM-dd}" 
                - Cuadro de metadatos corporativos: PARA: Liderazgo Ejecutivo | DE: Azure OpenAI Engine V3.5 | TEMA: Diagnóstico Operacional Avanzado.
                - Mencionar explícitamente el rango de fechas de ventas analizadas. 

            2. SECCIÓN 1: DESGLOSE DEL MARGEN DE BENEFICIO Y DE LOS COGS 
                - Tabla HTML perfectamente estilizada que calcula los márgenes en las ventas e ingredientes del período. 
                - Clasifica los productos usando Badges CSS de color según su estado de Ingeniería de Menú. 

            3. SECCIÓN 2: CADENA DE SUMINISTRO Y CLASIFICACIÓN DEL INVENTARIO 
                - Tarjetas independientes para cada ingrediente crítico (Stock <= NivelReorden) indicando el stock actual, stock mínimo y una "Acción Comercial Inmediata". 

            4. SECCIÓN 3: INGENIERÍA DEL MENÚ INMEDIATO Y AJUSTES DE PRECIOS 
                - Cajas de acción detallando pasos estratégicos para defender el margen objetivo del 68%. 

            5. PRÓXIMOS PASOS 
                - Sección de cierre con tareas accionables inmediatas.

            IMPORTANTE: No agregues texto explicativo fuera del HTML. No uses marcas Markdown (```html). Tu respuesta debe iniciar directamente con <!DOCTYPE html> y terminar con </html>.
            """;
    }

    // HTML-cleaning removed; responses are plain text.

    private async Task UpsertCacheAsync(
        Guid tenantId,
        DateOnly salesStartDate,
        DateOnly salesEndDate,
        DateOnly cacheDate,
        string html,
        DateTime generatedAtUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _db.StrategicAiReportCaches.FirstOrDefaultAsync(
            c =>
                c.TenantId == tenantId
                && c.SalesStartDate == salesStartDate
                && c.SalesEndDate == salesEndDate
                && c.CacheDate == cacheDate,
            cancellationToken);

        if (existing is null)
        {
            await _db.StrategicAiReportCaches.AddAsync(
                new StrategicAiReportCache
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SalesStartDate = salesStartDate,
                    SalesEndDate = salesEndDate,
                    CacheDate = cacheDate,
                    HtmlContent = html,
                    GeneratedAtUtc = generatedAtUtc,
                    CreatedAtUtc = generatedAtUtc,
                },
                cancellationToken);
        }
        else
        {
            existing.HtmlContent = html;
            existing.GeneratedAtUtc = generatedAtUtc;
            existing.UpdatedAtUtc = generatedAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // Gemini-related types removed. Azure OpenAI is used exclusively.

    private sealed record PaidSalesLineRow(
        string ProductName,
        decimal Quantity,
        decimal LineTotal,
        Guid SalesOrderId,
        DateTime CreatedAtUtc);
}
