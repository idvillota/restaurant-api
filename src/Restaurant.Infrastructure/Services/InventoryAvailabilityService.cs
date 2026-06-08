using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Inventory;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class InventoryAvailabilityService : IInventoryAvailabilityService
{
    private readonly ApplicationDbContext _db;

    public InventoryAvailabilityService(ApplicationDbContext db) => _db = db;

    public async Task<StockAvailabilityResultDto> CheckLinesAsync(
        StockAvailabilityCheckDto dto,
        CancellationToken cancellationToken = default)
    {
        var requirements = new Dictionary<Guid, decimal>();
        await AddDemandFromOpenOrdersAsync(requirements, dto.SalesOrderId, cancellationToken);
        await AddDemandFromLineDtosAsync(requirements, dto.Lines, cancellationToken);
        return await BuildResultAsync(requirements, cancellationToken);
    }

    public Task<StockAvailabilityResultDto> CheckKitchenBatchAsync(
        Guid salesOrderId,
        IReadOnlyList<AddSalesOrderLineDto> newLines,
        CancellationToken cancellationToken = default) =>
        CheckLinesAsync(
            new StockAvailabilityCheckDto
            {
                SalesOrderId = salesOrderId,
                Lines = newLines.Select(l => new StockCheckLineDto
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity,
                    ExcludedIngredientIds = l.ExcludedIngredientIds,
                }).ToList(),
            },
            cancellationToken);

    public async Task<StockAvailabilityResultDto> CheckOrdersForPaymentAsync(
        IReadOnlyList<Guid> salesOrderIds,
        CancellationToken cancellationToken = default)
    {
        var orders = await LoadOrdersWithLinesAsync(salesOrderIds, cancellationToken);
        return await CheckOrdersForPaymentFromEntitiesAsync(orders, cancellationToken);
    }

    public async Task<StockAvailabilityResultDto> CheckOrdersForPaymentFromEntitiesAsync(
        IReadOnlyList<SalesOrder> orders,
        CancellationToken cancellationToken = default)
    {
        var requirements = new Dictionary<Guid, decimal>();
        await AddDemandFromOrderEntitiesAsync(requirements, orders, cancellationToken);
        return await BuildResultAsync(requirements, cancellationToken);
    }

    public void EnsureAvailable(StockAvailabilityResultDto result)
    {
        if (result.IsAvailable)
            return;

        var details = string.Join(
            "; ",
            result.Shortages.Select(s =>
                $"{s.IngredientName}: faltan {s.Missing:N2} (disponible {s.Available:N2}, requiere {s.Required:N2})"));

        throw new InvalidOperationException(
            $"No hay inventario suficiente para completar la operación. {details}");
    }

    private async Task AddDemandFromOpenOrdersAsync(
        Dictionary<Guid, decimal> requirements,
        Guid? excludeSalesOrderId,
        CancellationToken cancellationToken)
    {
        var openOrderIds = await _db.SalesOrders
            .AsNoTracking()
            .Where(o => o.Status == SalesOrderStatus.Open)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        if (excludeSalesOrderId.HasValue)
            openOrderIds.Remove(excludeSalesOrderId.Value);

        if (openOrderIds.Count == 0)
            return;

        var lineSpecs = await _db.SalesOrderLines
            .AsNoTracking()
            .Where(l => openOrderIds.Contains(l.SalesOrderId) && l.SentToKitchenAtUtc != null)
            .Select(l => new
            {
                l.ProductId,
                l.Quantity,
                ExcludedIngredientIds = l.ExcludedIngredients.Select(e => e.IngredientId).ToList(),
            })
            .ToListAsync(cancellationToken);

        var specs = lineSpecs
            .Select(l => (l.ProductId, l.Quantity, l.ExcludedIngredientIds.ToHashSet()))
            .ToList();

        await AddDemandFromSpecsAsync(requirements, specs, cancellationToken);
    }

    private async Task<List<SalesOrder>> LoadOrdersWithLinesAsync(
        IReadOnlyList<Guid> salesOrderIds,
        CancellationToken cancellationToken) =>
        await _db.SalesOrders
            .AsNoTracking()
            .AsSplitQuery()
            .Where(o => salesOrderIds.Contains(o.Id))
            .Include(o => o.Lines)
            .ThenInclude(l => l.ExcludedIngredients)
            .ToListAsync(cancellationToken);

    private async Task AddDemandFromOrderEntitiesAsync(
        Dictionary<Guid, decimal> requirements,
        IReadOnlyList<SalesOrder> orders,
        CancellationToken cancellationToken)
    {
        var lineSpecs = orders
            .SelectMany(o => o.Lines.GroupBy(l => l.Id).Select(g => g.First()))
            .Where(l => l.SentToKitchenAtUtc is not null)
            .Select(l => (l.ProductId, l.Quantity, l.ExcludedIngredients.Select(e => e.IngredientId).ToHashSet()))
            .ToList();

        await AddDemandFromSpecsAsync(requirements, lineSpecs, cancellationToken);
    }

    private async Task AddDemandFromLineDtosAsync(
        Dictionary<Guid, decimal> requirements,
        IReadOnlyList<StockCheckLineDto> lines,
        CancellationToken cancellationToken)
    {
        var specs = lines.Select(l => (l.ProductId, l.Quantity, l.ExcludedIngredientIds.ToHashSet())).ToList();
        await AddDemandFromSpecsAsync(requirements, specs, cancellationToken);
    }

    private async Task AddDemandFromSpecsAsync(
        Dictionary<Guid, decimal> requirements,
        IReadOnlyList<(Guid ProductId, decimal Quantity, HashSet<Guid> Excluded)> specs,
        CancellationToken cancellationToken)
    {
        if (specs.Count == 0)
            return;

        var expansionSpecs = specs
            .Select(s => (s.ProductId, s.Quantity, s.Excluded))
            .ToList();

        await ProductInventoryExpansion.AddIngredientDemandAsync(_db, requirements, expansionSpecs, cancellationToken);
    }

    private async Task<StockAvailabilityResultDto> BuildResultAsync(
        Dictionary<Guid, decimal> requirements,
        CancellationToken cancellationToken)
    {
        if (requirements.Count == 0)
            return new StockAvailabilityResultDto { IsAvailable = true };

        var ingredientIds = requirements.Keys.ToList();
        var ingredients = await _db.Ingredients
            .AsNoTracking()
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var shortages = new List<IngredientShortageDto>();
        foreach (var (ingredientId, required) in requirements)
        {
            if (!ingredients.TryGetValue(ingredientId, out var ingredient))
                continue;

            var available = ingredient.StockQuantity ?? 0m;
            if (required <= available)
                continue;

            shortages.Add(
                new IngredientShortageDto
                {
                    IngredientId = ingredientId,
                    IngredientName = ingredient.Name,
                    Required = decimal.Round(required, 4),
                    Available = decimal.Round(available, 4),
                    Missing = decimal.Round(required - available, 4),
                });
        }

        return new StockAvailabilityResultDto
        {
            IsAvailable = shortages.Count == 0,
            Shortages = shortages,
        };
    }
}
