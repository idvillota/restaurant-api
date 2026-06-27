using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Common;

internal static class ProductCostCalculator
{
    internal static async Task<Dictionary<Guid, decimal>> GetCostPricesByProductIdsAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var compositionByProduct = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CompositionType })
            .ToDictionaryAsync(p => p.Id, p => p.CompositionType, cancellationToken);

        var recipeCosts = await (
            from pi in db.ProductIngredients.AsNoTracking()
            join ing in db.Ingredients.AsNoTracking() on pi.IngredientId equals ing.Id
            where productIds.Contains(pi.ProductId)
            select new { pi.ProductId, pi.Quantity, ing.UnitCost }
        ).ToListAsync(cancellationToken);

        var recipeCostByProduct = recipeCosts
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(r => ComputeLineCost(r.Quantity, r.UnitCost)));

        var bundleLines = await db.ProductBundleLines
            .AsNoTracking()
            .Where(bl => productIds.Contains(bl.ProductId))
            .Select(bl => new { bl.ProductId, bl.ComponentProductId, bl.Quantity })
            .ToListAsync(cancellationToken);

        var componentIds = bundleLines.Select(bl => bl.ComponentProductId).Distinct().ToList();
        var componentCosts = componentIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await GetCostPricesByProductIdsAsync(db, componentIds, cancellationToken);

        var bundleCostByProduct = bundleLines
            .GroupBy(bl => bl.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(bl => componentCosts.GetValueOrDefault(bl.ComponentProductId) * bl.Quantity));

        var result = new Dictionary<Guid, decimal>();
        foreach (var productId in productIds)
        {
            if (!compositionByProduct.TryGetValue(productId, out var compositionType))
                continue;

            result[productId] = compositionType switch
            {
                EProductType.Bundle => bundleCostByProduct.GetValueOrDefault(productId),
                _ => recipeCostByProduct.GetValueOrDefault(productId),
            };
        }

        return result;
    }

    private static decimal ComputeLineCost(decimal quantity, decimal? unitCost) =>
        quantity * (unitCost ?? 0m);
}
