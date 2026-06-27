using Restaurant.Application.Features.Dashboard;

namespace Restaurant.Application.Common.Interfaces;

public interface IDashboardService
{
    Task<DashboardLayoutDto> GetLayoutAsync(CancellationToken cancellationToken = default);

    Task<DashboardLayoutDto> UpdateLayoutAsync(
        DashboardLayoutDto layout,
        CancellationToken cancellationToken = default);

    IReadOnlyList<DashboardWidgetDefinitionDto> GetCatalog();
}
