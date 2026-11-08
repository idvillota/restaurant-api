using Restaurant.Application.Features.Cashier;

namespace Restaurant.Application.Common.Interfaces;

public interface IPlanillaPdfService
{
    Task<PlanillaGenerateResult> GenerateShiftPdfAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    Task<PlanillaGenerateResult> GenerateDailyPlanillaAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanillaListItemDto>> ListDailyPlanillasAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<GeneratedFileContent?> OpenDailyPlanillaAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<GeneratedFileContent?> OpenShiftPdfAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);
}
