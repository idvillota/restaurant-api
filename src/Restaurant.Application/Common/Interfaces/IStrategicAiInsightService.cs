using Restaurant.Application.Features.Reports;

namespace Restaurant.Application.Common.Interfaces;

public interface IStrategicAiInsightService
{
    Task<StrategicAiInsightsDto> GenerateInsightsAsync(
        StrategicJsonReportDocument document,
        CancellationToken cancellationToken = default);
}
