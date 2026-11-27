using System.Collections.Concurrent;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Infrastructure.Services;

public sealed class HostPrintInstallTicketStore : IHostPrintInstallTicketStore
{
    private readonly ConcurrentDictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);

    public void Issue(string token, byte[] setupExe, TimeSpan lifetime) =>
        _tickets[token] = new Ticket(DateTimeOffset.UtcNow.Add(lifetime), setupExe);

    public bool IsValid(string token)
    {
        if (!TryGet(token, out var ticket))
            return false;

        return ticket is not null;
    }

    public bool TryGetSetup(string token, out byte[] setupExe)
    {
        setupExe = [];
        if (!TryGet(token, out var ticket) || ticket is null)
            return false;

        setupExe = ticket.SetupExe;
        return setupExe.Length > 0;
    }

    private bool TryGet(string token, out Ticket? ticket)
    {
        ticket = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (!_tickets.TryGetValue(token, out var found))
            return false;

        if (found.Expires < DateTimeOffset.UtcNow)
        {
            _tickets.TryRemove(token, out _);
            return false;
        }

        ticket = found;
        return true;
    }

    private sealed record Ticket(DateTimeOffset Expires, byte[] SetupExe);
}
