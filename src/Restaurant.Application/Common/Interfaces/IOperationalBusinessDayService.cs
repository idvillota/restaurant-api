using Restaurant.Domain.Enums;

namespace Restaurant.Application.Common.Interfaces;

public sealed record EffectiveOperationalDay(
    DateOnly BusinessDate,
    DateOnly ClockBusinessDate,
    bool IsAdvancedBeyondClock,
    int OperationalDayCutoffHour,
    DailyClosureStatus ClosureStatus);

public interface IOperationalBusinessDayService
{
    Task<EffectiveOperationalDay> ResolveAsync(CancellationToken cancellationToken = default);
}
