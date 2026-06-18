using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Common;

internal static class SalesOrderLineCostSnapshot
{
    internal static async Task<Dictionary<Guid, decimal>> ApplyToOrdersAsync(
        ApplicationDbContext db,
        IEnumerable<SalesOrder> orders,
        CancellationToken cancellationToken)
    {
        var lines = orders
            .SelectMany(o => o.Lines)
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .ToList();

        if (lines.Count == 0)
            return new Dictionary<Guid, decimal>();

        var specs = lines
            .Select(l => new ProductLineCostCalculator.LineCostSpec(
                l.Id,
                l.ProductId,
                l.ExcludedIngredients.Select(e => e.IngredientId).ToHashSet()))
            .ToList();

        var unitCosts = await ProductLineCostCalculator.ComputeUnitCostsAsync(db, specs, cancellationToken);

        foreach (var line in lines)
        {
            if (!unitCosts.TryGetValue(line.Id, out var unitCost))
                continue;

            line.UnitCostPrice = unitCost;
            db.SalesOrderLines.Update(line);
        }

        return unitCosts;
    }
}
