using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Sales.LunchTickets;

public static class LunchTicketCheckoutRules
{
    public sealed record CheckoutLine(Guid ProductId, string ProductName, decimal Quantity, bool IsLunchTicketRedeemable);

    public sealed record PaymentLine(PaymentMethod Method, decimal Amount);

    public static bool UsesTiquetera(IEnumerable<PaymentLine> payments) =>
        payments.Any(p => p.Method == PaymentMethod.Tiquetera);

    public static void ValidatePayments(
        bool tiqueterasEnabled,
        IReadOnlyList<PaymentLine> payments,
        IReadOnlyList<CheckoutLine> lines,
        Guid? customerId,
        int remainingTickets)
    {
        var tiqueteraPayments = payments.Where(p => p.Method == PaymentMethod.Tiquetera).ToList();
        if (tiqueteraPayments.Count == 0)
        {
            foreach (var payment in payments)
            {
                if (payment.Amount <= 0)
                    throw new InvalidOperationException("Cada pago debe ser mayor que cero.");
            }

            return;
        }

        if (!tiqueterasEnabled)
            throw new InvalidOperationException("Este local no tiene tiqueteras habilitadas.");

        if (payments.Any(p => p.Method != PaymentMethod.Tiquetera))
            throw new InvalidOperationException(
                "El método Tiquetera no se puede combinar con otros métodos de pago.");

        if (customerId is null || customerId == Guid.Empty)
            throw new InvalidOperationException("Selecciona el cliente dueño de la tiquetera.");

        if (lines.Count == 0)
            throw new InvalidOperationException("No hay productos para cobrar con tiquetera.");

        if (lines.Any(l => !l.IsLunchTicketRedeemable))
            throw new InvalidOperationException(
                "Tiquetera solo permite pagar almuerzos (productos marcados como redimibles).");

        var ticketsNeeded = TicketsRequired(lines);
        if (ticketsNeeded <= 0)
            throw new InvalidOperationException("La cantidad de almuerzos a redimir no es válida.");

        if (remainingTickets < ticketsNeeded)
            throw new InvalidOperationException(
                $"La tiquetera no tiene cupos suficientes (disponibles: {remainingTickets}, requeridos: {ticketsNeeded}).");

        var paymentSum = decimal.Round(tiqueteraPayments.Sum(p => p.Amount), 2, MidpointRounding.AwayFromZero);
        if (paymentSum != 0m)
            throw new InvalidOperationException(
                "El pago con Tiquetera debe ser $0 porque la venta ya se registró al comprar el paquete.");
    }

    public static int TicketsRequired(IEnumerable<CheckoutLine> lines) =>
        (int)Math.Ceiling(lines.Sum(l => l.Quantity));

    public static void ValidateProductFlags(int? lunchTicketPackSize, bool isLunchTicketRedeemable)
    {
        if (lunchTicketPackSize is { } size)
        {
            if (size <= 0)
                throw new InvalidOperationException("El tamaño de la tiquetera debe ser mayor que cero.");
            if (isLunchTicketRedeemable)
                throw new InvalidOperationException(
                    "Un producto no puede ser paquete de tiquetera y almuerzo redimible a la vez.");
        }
    }
}
