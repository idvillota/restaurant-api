using Restaurant.Domain.Entities;

namespace Restaurant.Application.Common.Interfaces;

public sealed record CounterReadyNotificationResult(bool Sent, string? Error);

/// <summary>
/// Sends the single "order ready, pick it up at the counter" notice.
/// A failure must come back as <see cref="CounterReadyNotificationResult.Sent"/> false, never as an exception
/// that would undo marking the order ready.
/// </summary>
public interface ICounterReadyNotifier
{
    Task<CounterReadyNotificationResult> NotifyReadyAsync(
        SalesOrder order,
        string tradeName,
        CancellationToken cancellationToken = default);
}
