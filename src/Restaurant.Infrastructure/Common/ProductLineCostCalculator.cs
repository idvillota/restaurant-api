using Microsoft.EntityFrameworkCore;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Common;

internal static class ProductLineCostCalculator
{
    internal sealed record LineCostSpec(Guid LineId, Guid ProductId, HashSet<Guid> ExcludedIngredientIds);

    internal static async Task<Dictionary<Guid, decimal>> ComputeUnitCostsAsync(
        ApplicationDbContext db,
        IReadOnlyList<LineCostSpec> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
            return new Dictionary<Guid, decimal>();

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var snapshots = await ProductInventoryExpansion.LoadCompositionSnapshotsAsync(db, productIds, cancellationToken);

        var ingredientIds = CollectIngredientIds(snapshots.Values);
        var ingredientCosts = ingredientIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await db.Ingredients
                .AsNoTracking()
                .Where(i => ingredientIds.Contains(i.Id))
                .Select(i => new { i.Id, i.UnitCost })
                .ToDictionaryAsync(i => i.Id, i => i.UnitCost ?? 0m, cancellationToken);

        var result = new Dictionary<Guid, decimal>(lines.Count);
        foreach (var line in lines)
        {
            var unitCost = ComputeProductUnitCost(
                line.ProductId,
                multiplier: 1m,
                line.ExcludedIngredientIds,
                snapshots,
                ingredientCosts,
                []);

            result[line.LineId] = decimal.Round(unitCost, 2, MidpointRounding.AwayFromZero);
        }

        return result;
    }

    private static HashSet<Guid> CollectIngredientIds(IEnumerable<ProductInventoryExpansion.ProductCompositionSnapshot> snapshots)
    {
        var ids = new HashSet<Guid>();
        foreach (var snapshot in snapshots)
        {
            foreach (var (ingredientId, _) in snapshot.Recipe)
                ids.Add(ingredientId);
        }

        return ids;
    }

    private static decimal ComputeProductUnitCost(
        Guid productId,
        decimal multiplier,
        HashSet<Guid> excludedIngredientIds,
        IReadOnlyDictionary<Guid, ProductInventoryExpansion.ProductCompositionSnapshot> snapshots,
        IReadOnlyDictionary<Guid, decimal> ingredientUnitCosts,
        HashSet<Guid> visitedProducts)
    {
        if (!visitedProducts.Add(productId))
            return 0m;

        if (!snapshots.TryGetValue(productId, out var snapshot))
            return 0m;

        return snapshot.CompositionType switch
        {
            Domain.Enums.EProductType.Prepared or Domain.Enums.EProductType.Resale => snapshot.Recipe.Sum(
                row =>
                {
                    if (excludedIngredientIds.Contains(row.IngredientId))
                        return 0m;

                    var unitCost = ingredientUnitCosts.GetValueOrDefault(row.IngredientId);
                    return row.Quantity * multiplier * unitCost;
                }),
            Domain.Enums.EProductType.Bundle => snapshot.BundleLines.Sum(
                row => ComputeProductUnitCost(
                    row.ComponentProductId,
                    multiplier * row.Quantity,
                    [],
                    snapshots,
                    ingredientUnitCosts,
                    visitedProducts)),
            _ => 0m,
        };
    }
}
