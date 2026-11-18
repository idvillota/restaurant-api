using Restaurant.Application.Features.Sales.LunchTickets;
using Restaurant.Domain.Enums;

namespace Restaurant.Tests.Unit.Application;

public class LunchTicketCheckoutRulesTests
{
    [Fact]
    public void ValidatePayments_AllowsNormalCashPayment()
    {
        LunchTicketCheckoutRules.ValidatePayments(
            tiqueterasEnabled: false,
            payments: [new LunchTicketCheckoutRules.PaymentLine(PaymentMethod.Cash, 15_000m)],
            lines: [new LunchTicketCheckoutRules.CheckoutLine(Guid.NewGuid(), "Almuerzo", 1, true)],
            customerId: null,
            remainingTickets: 0);
    }

    [Fact]
    public void ValidatePayments_RejectsTiquetera_WhenFlagOff()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LunchTicketCheckoutRules.ValidatePayments(
                tiqueterasEnabled: false,
                payments: [new LunchTicketCheckoutRules.PaymentLine(PaymentMethod.Tiquetera, 0m)],
                lines: [new LunchTicketCheckoutRules.CheckoutLine(Guid.NewGuid(), "Almuerzo", 1, true)],
                customerId: Guid.NewGuid(),
                remainingTickets: 5));

        Assert.Contains("no tiene tiqueteras", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePayments_RejectsNonLunchProduct()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LunchTicketCheckoutRules.ValidatePayments(
                tiqueterasEnabled: true,
                payments: [new LunchTicketCheckoutRules.PaymentLine(PaymentMethod.Tiquetera, 0m)],
                lines:
                [
                    new LunchTicketCheckoutRules.CheckoutLine(Guid.NewGuid(), "Almuerzo", 1, true),
                    new LunchTicketCheckoutRules.CheckoutLine(Guid.NewGuid(), "Jugo", 1, false),
                ],
                customerId: Guid.NewGuid(),
                remainingTickets: 5));

        Assert.Contains("solo permite pagar almuerzos", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePayments_RejectsWhenInsufficientTickets()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LunchTicketCheckoutRules.ValidatePayments(
                tiqueterasEnabled: true,
                payments: [new LunchTicketCheckoutRules.PaymentLine(PaymentMethod.Tiquetera, 0m)],
                lines: [new LunchTicketCheckoutRules.CheckoutLine(Guid.NewGuid(), "Almuerzo", 2, true)],
                customerId: Guid.NewGuid(),
                remainingTickets: 1));

        Assert.Contains("cupos suficientes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePayments_AcceptsValidRedemption()
    {
        LunchTicketCheckoutRules.ValidatePayments(
            tiqueterasEnabled: true,
            payments: [new LunchTicketCheckoutRules.PaymentLine(PaymentMethod.Tiquetera, 0m)],
            lines: [new LunchTicketCheckoutRules.CheckoutLine(Guid.NewGuid(), "Almuerzo", 1, true)],
            customerId: Guid.NewGuid(),
            remainingTickets: 19);
    }

    [Fact]
    public void ValidateProductFlags_RejectsPackAndRedeemableTogether()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LunchTicketCheckoutRules.ValidateProductFlags(20, true));
    }

    [Fact]
    public void TicketsRequired_CeilingsFractionalQuantities()
    {
        Assert.Equal(2, LunchTicketCheckoutRules.TicketsRequired(
        [
            new LunchTicketCheckoutRules.CheckoutLine(Guid.NewGuid(), "Almuerzo", 1.5m, true),
        ]));
    }
}
