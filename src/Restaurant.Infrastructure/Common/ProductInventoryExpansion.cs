using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Common;

internal static class ProductInventoryExpansion
{
    internal sealed class ProductCompositionSnapshot
    {
        public required EProductType CompositionType { get; init; }
        public required IReadOnlyList<(Guid IngredientId, decimal Quantity)> Recipe { get; init; }
        public required IReadOnlyList<(Guid ComponentProductId, decimal Quantity)> BundleLines { get; init; }
    }

    internal static async Task AddIngredientDemandAsync(
        ApplicationDbContext db,
        Dictionary<Guid, decimal> requirements,
        IReadOnlyList<(Guid ProductId, decimal LineQuantity, HashSet<Guid> ExcludedIngredientIds)> specs,
        CancellationToken cancellationToken)
    {
        if (specs.Count == 0)
            return;

        var rootProductIds = specs.Select(s => s.ProductId).Distinct().ToList();
        var snapshots = await LoadCompositionSnapshotsAsync(db, rootProductIds, cancellationToken);

        foreach (var (productId, lineQuantity, excluded) in specs)
        {
            ExpandProductDemand(
                requirements,
                productId,
                lineQuantity,
                excluded,
                snapshots,
                []);
        }
    }

    private static void ExpandProductDemand(
        Dictionary<Guid, decimal> requirements,
        Guid productId,
        decimal multiplier,
        HashSet<Guid> excludedIngredientIds,
        IReadOnlyDictionary<Guid, ProductCompositionSnapshot> snapshots,
        HashSet<Guid> visitedProducts)
    {
        if (!visitedProducts.Add(productId))
            return;

        if (!snapshots.TryGetValue(productId, out var snapshot))
            return;

        switch (snapshot.CompositionType)
        {
            case EProductType.Prepared:
            case EProductType.Resale:
                foreach (var (ingredientId, quantity) in snapshot.Recipe)
                {
                    if (excludedIngredientIds.Contains(ingredientId))
                        continue;

                    var needed = quantity * multiplier;
                    requirements[ingredientId] = requirements.GetValueOrDefault(ingredientId) + needed;
                }

                break;

            case EProductType.Bundle:
                foreach (var (componentProductId, componentQuantity) in snapshot.BundleLines)
                {
                    ExpandProductDemand(
                        requirements,
                        componentProductId,
                        multiplier * componentQuantity,
                        [],
                        snapshots,
                        visitedProducts);
                }

                break;
        }
    }

    private static async Task<Dictionary<Guid, ProductCompositionSnapshot>> LoadCompositionSnapshotsAsync(
        ApplicationDbContext db,
        IReadOnlyList<Guid> rootProductIds,
        CancellationToken cancellationToken)
    {
        var pending = new Queue<Guid>(rootProductIds);
        var seen = new HashSet<Guid>();
        var products = new Dictionary<Guid, (EProductType CompositionType, List<(Guid, decimal)> Recipe, List<(Guid, decimal)> Bundle)>();

        while (pending.Count > 0)
        {
            var batch = new List<Guid>();
            while (pending.Count > 0 && batch.Count < 50)
            {
                var id = pending.Dequeue();
                if (seen.Add(id))
                    batch.Add(id);
            }

            if (batch.Count == 0)
                continue;

            var productRows = await db.Products
                .AsNoTracking()
                .Where(p => batch.Contains(p.Id))
                .Select(p => new { p.Id, p.CompositionType })
                .ToListAsync(cancellationToken);

            foreach (var row in productRows)
            {
                products[row.Id] = (row.CompositionType, [], []);
            }

            var recipeRows = await db.ProductIngredients
                .AsNoTracking()
                .Where(pi => batch.Contains(pi.ProductId))
                .Select(pi => new { pi.ProductId, pi.IngredientId, pi.Quantity })
                .ToListAsync(cancellationToken);

            foreach (var row in recipeRows)
            {
                if (products.TryGetValue(row.ProductId, out var entry))
                    entry.Recipe.Add((row.IngredientId, row.Quantity));
            }

            var bundleRows = await db.ProductBundleLines
                .AsNoTracking()
                .Where(bl => batch.Contains(bl.ProductId))
                .Select(bl => new { bl.ProductId, bl.ComponentProductId, bl.Quantity })
                .ToListAsync(cancellationToken);

            foreach (var row in bundleRows)
            {
                if (products.TryGetValue(row.ProductId, out var entry))
                {
                    entry.Bundle.Add((row.ComponentProductId, row.Quantity));
                    if (seen.Add(row.ComponentProductId))
                        pending.Enqueue(row.ComponentProductId);
                }
            }
        }

        return products.ToDictionary(
            kvp => kvp.Key,
            kvp => new ProductCompositionSnapshot
            {
                CompositionType = kvp.Value.CompositionType,
                Recipe = kvp.Value.Recipe,
                BundleLines = kvp.Value.Bundle,
            });
    }
}
