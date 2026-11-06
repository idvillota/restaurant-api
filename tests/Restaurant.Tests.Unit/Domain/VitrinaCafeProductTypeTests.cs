using Restaurant.Domain.Sales;

namespace Restaurant.Tests.Unit.Domain;

public sealed class VitrinaCafeProductTypeTests
{
    [Theory]
    [InlineData("Vitrina Café", true)]
    [InlineData("vitrina café", true)]
    [InlineData("  Vitrina Café  ", true)]
    [InlineData("Bebidas", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Matches_product_type_name(string? name, bool expected) =>
        Assert.Equal(expected, VitrinaCafeProductType.Matches(name));

    [Fact]
    public void SplitCollected_keeps_vitrina_out_of_general_sales()
    {
        var (general, vitrina) = VitrinaCafeProductType.SplitCollected(100m, 30m);
        Assert.Equal(70m, general);
        Assert.Equal(30m, vitrina);
    }
}
