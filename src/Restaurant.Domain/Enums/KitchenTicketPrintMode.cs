namespace Restaurant.Domain.Enums;

/// <summary>
/// Controls whether a kitchen send generates PDF/XML and a print job.
/// Confirm still marks lines sent; this only gates paper/files.
/// </summary>
public enum KitchenTicketPrintMode
{
    Always = 0,
    Never = 1,
    Ask = 2,
}

public static class KitchenTicketPrintDecision
{
    /// <summary>
    /// Always prints. Never skips. Ask uses the waiter flag; omitted flag prints
    /// so old clients and Host Lite do not silently stop tickets.
    /// </summary>
    public static bool ShouldGenerateTickets(KitchenTicketPrintMode mode, bool? requested) =>
        mode switch
        {
            KitchenTicketPrintMode.Never => false,
            KitchenTicketPrintMode.Ask => requested ?? true,
            _ => true,
        };
}
