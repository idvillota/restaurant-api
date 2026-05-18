namespace Restaurant.Application.Common;

public static class InventoryCosting
{
    public static decimal ComputeWeightedAverageUnitCost(
        decimal? currentQuantity,
        decimal? currentUnitCost,
        decimal purchaseQuantity,
        decimal purchaseUnitPrice)
    {
        if (purchaseQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(purchaseQuantity), "Purchase quantity must be greater than zero.");

        if (purchaseUnitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(purchaseUnitPrice), "Purchase unit price cannot be negative.");

        var oldQty = currentQuantity ?? 0m;
        if (oldQty <= 0)
            return purchaseUnitPrice;

        var oldCost = currentUnitCost ?? 0m;
        var newQty = oldQty + purchaseQuantity;
        return (oldQty * oldCost + purchaseQuantity * purchaseUnitPrice) / newQty;
    }

    public static decimal AddStock(decimal? currentQuantity, decimal purchaseQuantity) =>
        (currentQuantity ?? 0m) + purchaseQuantity;
}
