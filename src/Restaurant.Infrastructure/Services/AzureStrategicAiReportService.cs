using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.Reports;
using Restaurant.Infrastructure.Persistence;
using System.Net.Http;
using System.Net.Http.Json;

namespace Restaurant.Infrastructure.Services;

/// <summary>
/// Implementation of IStrategicAiReportService using Azure OpenAI (Chat Completions).
/// Reuses the same data-context builders from the original service and focuses on
/// integrating with Azure's OpenAI via OpenAIClient.
/// </summary>
public sealed class AzureStrategicAiReportService : IStrategicAiReportService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenant;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureOpenAiOptions _options;
    private readonly ILogger<AzureStrategicAiReportService> _logger;

    public AzureStrategicAiReportService(
        ApplicationDbContext db,
        ICurrentTenantContext tenant,
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureStrategicAiReportService> logger)
    {
        _db = db;
        _tenant = tenant;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
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

    // The following helper methods largely mirror the logic from the Gemini-based service.
    // For brevity they call into the same private helpers defined in the original StrategicAiReportService
    // file (BuildInventoryContextAsync, BuildSalesContextAsync, UpsertCacheAsync, CleanHtml).

    private async Task<string> BuildInventoryContextAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // Reuse logic from original service via manual repeat (simplified here to avoid file coupling).
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

        var paidLineRows = await _db.SalesOrderLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId
                && l.CreatedAtUtc >= startUtc
                && l.CreatedAtUtc < endExclusive
                && l.SalesOrder.Status == Domain.Enums.SalesOrderStatus.Paid)
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
        if (string.IsNullOrWhiteSpace(_options.DeploymentName))
            throw new InvalidOperationException("AzureOpenAi:DeploymentName no está configurado.");

        var prompt = BuildMasterPrompt(inventoryContext, salesContext, salesStartDate, salesEndDate);

        // Simple guard: truncate if exceeds configured max prompt length
        if (prompt.Length > _options.MaxPromptChars)
        {
            _logger.LogWarning("Prompt demasiado largo ({Length} chars). Se aplicará truncado seguro.", prompt.Length);
            var maxPerContext = Math.Max(16_000, _options.MaxPromptChars / 3);
            inventoryContext = inventoryContext.Length > maxPerContext
                ? inventoryContext.Substring(0, maxPerContext)
                : inventoryContext;
            salesContext = salesContext.Length > maxPerContext
                ? salesContext.Substring(0, maxPerContext)
                : salesContext;
            prompt = BuildMasterPrompt(inventoryContext, salesContext, salesStartDate, salesEndDate);
        }

        var deployment = _options.DeploymentName.Trim();

        var payload = new
        {
            messages = new[]
            {
                // Request plain text only (no HTML, no Markdown)
                new { role = "system", content = "Actúa como un analista de datos. Responde únicamente con TEXTO PLANO en español. No uses HTML, etiquetas, ni Markdown. Entrega la respuesta como texto limpio y estructurado." },
                new { role = "user", content = prompt }
            },
            max_tokens = 8000,
            temperature = 0.2f
        };

        var client = _httpClientFactory.CreateClient("AzureOpenAiClient");
        var azureUrlRelative = $"/openai/deployments/{deployment}/chat/completions?api-version=2023-05-15";

        // If the configured BaseAddress already contains the OpenAI path (e.g. "/openai/v1" or "/openai"),
        // avoid duplicating the segment when building the request URL.
        string requestUri;
        if (client.BaseAddress is null)
        {
            requestUri = azureUrlRelative;
        }
        else
        {
            var basePath = client.BaseAddress.AbsolutePath.TrimEnd('/').ToLowerInvariant();
            if (basePath.Contains("/openai") || basePath.Contains("/openai/v1"))
                requestUri = $"deployments/{deployment}/chat/completions?api-version=2023-05-15";
            else
                requestUri = azureUrlRelative;

            // Build absolute URI for logging and request
            requestUri = new Uri(client.BaseAddress, requestUri).ToString();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        // Log request metadata (not API key) to help debugging
        try
        {
            _logger.LogInformation("Azure OpenAI request -> {FullUrl} (deployment={Deployment})", requestUri, deployment);

            // Check whether the named HttpClient has the api-key header configured
            if (client.DefaultRequestHeaders.Contains("api-key"))
            {
                var hdr = client.DefaultRequestHeaders.GetValues("api-key").FirstOrDefault() ?? string.Empty;
                var masked = hdr.Length > 8 ? hdr.Substring(0, 4) + new string('*', hdr.Length - 8) + hdr.Substring(hdr.Length - 4) : "***masked***";
                _logger.LogDebug("AzureOpenAiClient has api-key configured (masked)={ApiKeyMasked}", masked);
            }
            else
            {
                _logger.LogWarning("AzureOpenAiClient does not have an 'api-key' header configured.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while logging Azure OpenAI request metadata");
        }

        // The HttpClient `AzureOpenAiClient` is configured with the BaseAddress and
        // should include the required `api-key` header. Send the request as-is.
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

        // Return plain trimmed text (no HTML processing)
        return content.Trim();
    }

    private static string BuildMasterPrompt(
        string inventoryContext,
        string salesContext,
        DateOnly salesStartDate,
        DateOnly salesEndDate)
    {
        return $"""
            Eres un analista de datos de un restaurante.
            Analiza los datos de la base de datos
            Responde SOLO con una lista conteniendo:
            
            1. Dame un listado de ingredientes del inventoryContext
            """;
    }

    // Responses are plain text; HTML-cleaning functions removed.

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
                new Domain.Entities.StrategicAiReportCache
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

    private sealed record PaidSalesLineRow(
        string ProductName,
        decimal Quantity,
        decimal LineTotal,
        Guid SalesOrderId,
        DateTime CreatedAtUtc);
}
