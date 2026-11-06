using Restaurant.Domain.Enums;

namespace Restaurant.Tests.Unit.Domain;

public sealed class KitchenTicketPrintDecisionTests
{
    [Theory]
    [InlineData(KitchenTicketPrintMode.Always, null, true)]
    [InlineData(KitchenTicketPrintMode.Always, true, true)]
    [InlineData(KitchenTicketPrintMode.Always, false, true)]
    [InlineData(KitchenTicketPrintMode.Never, null, false)]
    [InlineData(KitchenTicketPrintMode.Never, true, false)]
    [InlineData(KitchenTicketPrintMode.Never, false, false)]
    [InlineData(KitchenTicketPrintMode.Ask, null, true)]
    [InlineData(KitchenTicketPrintMode.Ask, true, true)]
    [InlineData(KitchenTicketPrintMode.Ask, false, false)]
    public void ShouldGenerateTickets_matches_mode_and_request(
        KitchenTicketPrintMode mode,
        bool? requested,
        bool expected)
    {
        Assert.Equal(expected, KitchenTicketPrintDecision.ShouldGenerateTickets(mode, requested));
    }
}
