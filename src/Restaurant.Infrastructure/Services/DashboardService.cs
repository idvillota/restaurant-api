using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Dashboard;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public DashboardService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<DashboardLayoutDto> GetLayoutAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        return DeserializeLayout(settings.DashboardLayoutJson) ?? DefaultDashboardLayout.Create();
    }

    public async Task<DashboardLayoutDto> UpdateLayoutAsync(
        DashboardLayoutDto layout,
        CancellationToken cancellationToken = default)
    {
        ValidateLayout(layout);

        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        settings.DashboardLayoutJson = JsonSerializer.Serialize(layout, JsonOptions);
        _db.TenantSettings.Update(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return layout;
    }

    public IReadOnlyList<DashboardWidgetDefinitionDto> GetCatalog() => DashboardCatalog.Widgets;

    private static void ValidateLayout(DashboardLayoutDto layout)
    {
        if (layout.Panels.Count > 24)
            throw new InvalidOperationException("El panel admite como máximo 24 widgets.");

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var panel in layout.Panels)
        {
            if (string.IsNullOrWhiteSpace(panel.Id))
                throw new InvalidOperationException("Cada widget debe tener un identificador.");

            if (!seenIds.Add(panel.Id))
                throw new InvalidOperationException($"Identificador duplicado: {panel.Id}.");

            if (!DashboardCatalog.ByType.TryGetValue(panel.WidgetType, out var definition))
                throw new InvalidOperationException($"Tipo de widget desconocido: {panel.WidgetType}.");

            if (panel.W < definition.MinWidth || panel.H < definition.MinHeight)
                throw new InvalidOperationException(
                    $"El widget «{definition.Name}» requiere al menos {definition.MinWidth}×{definition.MinHeight} celdas.");

            if (panel.X < 0 || panel.Y < 0 || panel.W < 1 || panel.H < 1)
                throw new InvalidOperationException("Posición o tamaño de widget inválido.");

            if (panel.X + panel.W > 12)
                throw new InvalidOperationException($"El widget «{definition.Name}» se sale del ancho del panel.");
        }
    }

    private static DashboardLayoutDto? DeserializeLayout(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<DashboardLayoutDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<TenantSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (settings is not null)
            return settings;

        settings = new TenantSettings
        {
            TenantId = tenantId,
            MaxDiscountPercent = 10m,
            OperationalDayCutoffHour = 4,
            ImpoconsumoPercent = 8m,
            TaxRegime = "Régimen Simplificado",
            Country = "Colombia",
        };
        await _db.TenantSettings.AddAsync(settings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private Guid ResolveTenantId()
    {
        if (_tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }
}
