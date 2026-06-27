using Restaurant.Application.Features.Reports;

namespace Restaurant.Application.Common.Interfaces;

public interface IStrategicAnalyticsService
{
    Task<StrategicJsonReportDocument> GetReportAsync(
        string reportType,
        DateOnly startDate,
        DateOnly endDate,
        int? forecastDays,
        bool refresh,
        bool includeAiInsights,
        CancellationToken cancellationToken = default);
}
