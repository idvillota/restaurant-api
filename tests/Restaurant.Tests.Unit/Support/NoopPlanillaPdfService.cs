using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;

namespace Restaurant.Tests.Unit.Support;

internal sealed class NoopPlanillaPdfService : IPlanillaPdfService
{
    public Task<PlanillaGenerateResult> GenerateShiftPdfAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PlanillaGenerateResult { RelativePath = "Planillas/noop.pdf" });

    public Task<PlanillaGenerateResult> GenerateDailyPlanillaAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PlanillaGenerateResult { RelativePath = "Planillas/noop.pdf" });

    public Task<IReadOnlyList<PlanillaListItemDto>> ListDailyPlanillasAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlanillaListItemDto>>([]);

    public Task<GeneratedFileContent?> OpenDailyPlanillaAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<GeneratedFileContent?>(null);

    public Task<GeneratedFileContent?> OpenShiftPdfAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<GeneratedFileContent?>(null);
}
