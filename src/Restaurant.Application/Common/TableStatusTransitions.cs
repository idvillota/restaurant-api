using Restaurant.Domain.Enums;

namespace Restaurant.Application.Common;

public static class TableStatusTransitions
{
    public static bool CanTransition(ETableStatus from, ETableStatus to)
    {
        if (from == to)
            return true;

        return (from, to) switch
        {
            (ETableStatus.Available, ETableStatus.Reserved) => true,
            (ETableStatus.Available, ETableStatus.Busy) => true,
            (ETableStatus.Reserved, ETableStatus.Available) => true,
            (ETableStatus.Reserved, ETableStatus.Busy) => true,
            (ETableStatus.Busy, ETableStatus.Available) => true,
            _ => false,
        };
    }

    public static void EnsureCanTransition(ETableStatus from, ETableStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Cannot change table status from {from} to {to}.");
    }

    public static bool IsTerminalReservationRelease(ReservationStatus status) =>
        status is ReservationStatus.Completed or ReservationStatus.Cancelled or ReservationStatus.NoShow;
}
