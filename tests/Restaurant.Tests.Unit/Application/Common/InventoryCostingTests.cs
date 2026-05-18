using Restaurant.Application.Common;

namespace Restaurant.Tests.Unit.Application.Common;

public sealed class InventoryCostingTests
{
    [Fact]
    public void ComputeWeightedAverageUnitCost_uses_purchase_price_when_no_stock()
    {
        var cost = InventoryCosting.ComputeWeightedAverageUnitCost(null, null, 10m, 3m);
        Assert.Equal(3m, cost);
    }

    [Fact]
    public void ComputeWeightedAverageUnitCost_blends_existing_and_new_stock()
    {
        var cost = InventoryCosting.ComputeWeightedAverageUnitCost(10m, 2m, 10m, 4m);
        Assert.Equal(3m, cost);
    }

    [Fact]
    public void AddStock_treats_null_as_zero()
    {
        Assert.Equal(7m, InventoryCosting.AddStock(null, 7m));
    }
}
