using Restaurant.Application.Features.Sales.KitchenTickets;

namespace Restaurant.Tests.Unit.Application;

public sealed class KitchenTicketDeliveryLineTests
{
    [Fact]
    public void FormatDeliveryDestination_joins_dialog_address_and_phone()
    {
        var line = KitchenTicketModel.FormatDeliveryDestination("MZ 7 Casa 13 La minga", "30234234");

        Assert.Equal("MZ 7 Casa 13 La minga - 30234234", line);
    }

    [Theory]
    [InlineData("Casa de mamá", null, "Casa de mamá")]
    [InlineData(null, "30234234", "30234234")]
    [InlineData("  ", "  ", null)]
    [InlineData(null, null, null)]
    public void FormatDeliveryDestination_omits_blank_parts(string? address, string? phone, string? expected)
    {
        Assert.Equal(expected, KitchenTicketModel.FormatDeliveryDestination(address, phone));
    }
}
