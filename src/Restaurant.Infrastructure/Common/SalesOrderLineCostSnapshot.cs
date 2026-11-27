using Microsoft.EntityFrameworkCore;
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

        var lineIds = lines.Select(l => l.Id).ToList();
        var picks = await db.SalesOrderLineChoices.AsNoTracking()
            .Where(c => lineIds.Contains(c.SalesOrderLineId))
            .Select(c => new { c.SalesOrderLineId, c.ProductId })
            .ToListAsync(cancellationToken);
        var byLine = picks
            .GroupBy(c => c.SalesOrderLineId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProductId).ToList());

        var specs = new List<ProductLineCostCalculator.LineCostSpec>();
        var choiceParts = new Dictionary<Guid, List<Guid>>();
        foreach (var line in lines)
        {
            if (byLine.TryGetValue(line.Id, out var chosen) && chosen.Count > 0)
            {
                var partIds = new List<Guid>();
                foreach (var productId in chosen)
                {
                    var partId = Guid.NewGuid();
                    partIds.Add(partId);
                    specs.Add(new ProductLineCostCalculator.LineCostSpec(partId, productId, []));
                }

                choiceParts[line.Id] = partIds;
                continue;
            }

            specs.Add(new ProductLineCostCalculator.LineCostSpec(
                line.Id,
                line.ProductId,
                line.ExcludedIngredients.Select(e => e.IngredientId).ToHashSet()));
        }

        var unitCosts = await ProductLineCostCalculator.ComputeUnitCostsAsync(db, specs, cancellationToken);

        foreach (var line in lines)
        {
            decimal unitCost;
            if (choiceParts.TryGetValue(line.Id, out var partIds))
                unitCost = partIds.Sum(id => unitCosts.GetValueOrDefault(id));
            else if (!unitCosts.TryGetValue(line.Id, out unitCost))
                continue;

            line.UnitCostPrice = unitCost;
            db.SalesOrderLines.Update(line);
        }

        return unitCosts;
    }
}
