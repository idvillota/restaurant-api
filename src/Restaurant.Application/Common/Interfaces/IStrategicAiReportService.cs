using Restaurant.Application.Features.Reports;

namespace Restaurant.Application.Common.Interfaces;

public interface IStrategicAiReportService
{
    Task<StrategicReportDto> GetStrategicReportAsync(
        DateOnly salesStartDate,
        DateOnly salesEndDate,
        bool refresh,
        CancellationToken cancellationToken = default);
}
