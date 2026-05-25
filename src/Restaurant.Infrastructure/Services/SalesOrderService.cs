using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;

namespace Restaurant.Infrastructure.Services;

public sealed class SalesOrderService : ISalesOrderService
{
    private readonly IRepository<SalesOrder> _orders;
    private readonly IRepository<SalesOrderLine> _lines;
    private readonly IRepository<SalesOrderLineExcludedIngredient> _excluded;
    private readonly IRepository<DiningTable> _tables;
    private readonly IRepository<Product> _products;
    private readonly IRepository<ProductIngredient> _productIngredients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IInventoryAvailabilityService _inventory;
    private readonly IKitchenTicketService _kitchenTickets;
    private readonly IOperationalBusinessDayService _operationalDay;

    public SalesOrderService(
        IRepository<SalesOrder> orders,
        IRepository<SalesOrderLine> lines,
        IRepository<SalesOrderLineExcludedIngredient> excluded,
        IRepository<DiningTable> tables,
        IRepository<Product> products,
        IRepository<ProductIngredient> productIngredients,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IInventoryAvailabilityService inventory,
        IKitchenTicketService kitchenTickets,
        IOperationalBusinessDayService operationalDay)
    {
        _orders = orders;
        _lines = lines;
        _excluded = excluded;
        _tables = tables;
        _products = products;
        _productIngredients = productIngredients;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _inventory = inventory;
        _kitchenTickets = kitchenTickets;
        _operationalDay = operationalDay;
    }

    public async Task<IReadOnlyList<TableServiceSummaryDto>> ListTableSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var tables = await _tables.Query()
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Zone)
            .ThenBy(t => t.Code)
            .ToListAsync(cancellationToken);

        var openOrders = await _orders.Query()
            .AsNoTracking()
            .Where(o => o.DiningTableId != null &&
                        (o.Status == SalesOrderStatus.Draft || o.Status == SalesOrderStatus.Open))
            .Select(o => new { o.Id, o.DiningTableId, o.Number, o.Total })
            .ToListAsync(cancellationToken);

        var orderIds = openOrders.Select(o => o.Id).ToList();
        var pendingCountByOrder = orderIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _lines.Query()
                .AsNoTracking()
                .Where(l => orderIds.Contains(l.SalesOrderId) && l.SentToKitchenAtUtc == null)
                .GroupBy(l => l.SalesOrderId)
                .Select(g => new { OrderId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OrderId, x => x.Count, cancellationToken);

        var orderByTable = openOrders
            .Where(o => o.DiningTableId.HasValue)
            .ToDictionary(o => o.DiningTableId!.Value);

        return tables
            .Select(t =>
            {
                orderByTable.TryGetValue(t.Id, out var order);
                var pendingKitchen = order is not null && pendingCountByOrder.TryGetValue(order.Id, out var c)
                    ? c
                    : 0;
                return new TableServiceSummaryDto
                {
                    TableId = t.Id,
                    Code = t.Code,
                    Zone = t.Zone,
                    Status = t.Status,
                    Capacity = t.Capacity,
                    LayoutX = t.LayoutX,
                    LayoutY = t.LayoutY,
                    OpenOrderId = order?.Id,
                    OpenOrderNumber = order?.Number,
                    OpenOrderTotal = order?.Total,
                    OpenOrderPendingKitchenLineCount = pendingKitchen,
                };
            })
            .ToList();
    }

    public Task<SalesOrderDto?> GetOpenByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default) =>
        LoadOrderDtoAsync(
            o => o.DiningTableId == tableId &&
                 (o.Status == SalesOrderStatus.Draft || o.Status == SalesOrderStatus.Open),
            cancellationToken);

    public Task<SalesOrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        LoadOrderDtoAsync(o => o.Id == id, cancellationToken);

    public async Task<SalesOrderDto> StartOrderForTableAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        await EnsureSalonOperationsAllowedAsync(cancellationToken);

        var table = await _tables.GetByIdAsync(tableId, cancellationToken);
        if (table is null || !table.IsActive)
            throw new InvalidOperationException("Table was not found or is inactive.");

        var existingActive = await _orders.Query()
            .AnyAsync(
                o => o.DiningTableId == tableId &&
                     (o.Status == SalesOrderStatus.Draft || o.Status == SalesOrderStatus.Open),
                cancellationToken);
        if (existingActive)
            throw new InvalidOperationException("This table already has an active order.");

        if (table.Status != ETableStatus.Busy)
        {
            TableStatusTransitions.EnsureCanTransition(table.Status, ETableStatus.Busy);
            table.Status = ETableStatus.Busy;
            _tables.Update(table);
        }

        var orderId = Guid.NewGuid();
        var number = await GenerateOrderNumberAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var order = new SalesOrder
        {
            Id = orderId,
            DiningTableId = tableId,
            Number = number,
            Status = SalesOrderStatus.Draft,
            OpenedAtUtc = now,
            Subtotal = 0,
            TaxAmount = 0,
            Total = 0,
        };

        await _orders.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(orderId, cancellationToken))!;
    }

    public async Task<SalesOrderDto?> AddLineAsync(
        Guid orderId,
        AddSalesOrderLineDto dto,
        CancellationToken cancellationToken = default)
    {
        await EnsureSalonOperationsAllowedAsync(cancellationToken);

        var order = await OrderWithLinesQuery(tracked: true)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return null;

        if (order.Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Open))
            throw new InvalidOperationException("Only active table orders can receive lines.");

        await ApplyLineToOrderAsync(order, dto, cancellationToken);
        RecalculateOrderTotals(order);
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(orderId, cancellationToken);
    }

    public async Task<SalesOrderDto?> RemovePendingLineAsync(
        Guid orderId,
        Guid lineId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSalonOperationsAllowedAsync(cancellationToken);

        var order = await OrderWithLinesQuery(tracked: true)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return null;

        if (order.Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Open))
            throw new InvalidOperationException("Only active table orders can be modified.");

        var line = DistinctLines(order.Lines).FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            throw new InvalidOperationException("Line was not found on this order.");

        if (line.SentToKitchenAtUtc is not null)
            throw new InvalidOperationException("Lines already sent to the kitchen cannot be removed.");

        order.Lines.Remove(line);
        _lines.Remove(line);
        RecalculateOrderTotals(order);
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(orderId, cancellationToken);
    }

    public async Task<SalesOrderDto?> UpdatePendingLineQuantityAsync(
        Guid orderId,
        Guid lineId,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        await EnsureSalonOperationsAllowedAsync(cancellationToken);

        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero.");

        var order = await OrderWithLinesQuery(tracked: true)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return null;

        if (order.Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Open))
            throw new InvalidOperationException("Only active table orders can be modified.");

        var line = DistinctLines(order.Lines).FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            throw new InvalidOperationException("Line was not found on this order.");

        if (line.SentToKitchenAtUtc is not null)
            throw new InvalidOperationException("Lines already sent to the kitchen cannot be changed.");

        line.Quantity = quantity;
        line.LineTotal = decimal.Round(quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero);
        _lines.Update(line);
        RecalculateOrderTotals(order);
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(orderId, cancellationToken);
    }

    public async Task<ConfirmSalesOrderResultDto?> ConfirmOrderAsync(
        Guid orderId,
        ConfirmSalesOrderDto dto,
        CancellationToken cancellationToken = default)
    {
        await EnsureSalonOperationsAllowedAsync(cancellationToken);

        var order = await OrderWithLinesQuery(tracked: true)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return null;

        if (order.Status == SalesOrderStatus.Paid || order.Status == SalesOrderStatus.Voided)
            throw new InvalidOperationException("Paid or voided orders cannot be modified.");

        if (order.Status != SalesOrderStatus.Draft && order.Status != SalesOrderStatus.Open)
            throw new InvalidOperationException("Only active table orders can be sent to the kitchen.");

        var pendingLines = DistinctLines(order.Lines)
            .Where(l => l.SentToKitchenAtUtc is null)
            .ToList();

        if (pendingLines.Count == 0)
            throw new InvalidOperationException("No hay productos pendientes de envío a cocina.");

        var batchDtos = pendingLines
            .Select(l => new AddSalesOrderLineDto
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                Notes = l.Notes,
                ExcludedIngredientIds = l.ExcludedIngredients
                    .Select(e => e.IngredientId)
                    .OrderBy(id => id)
                    .ToList(),
            })
            .ToList();

        var stockCheck = await _inventory.CheckKitchenBatchAsync(orderId, batchDtos, cancellationToken);
        _inventory.EnsureAvailable(stockCheck);

        var sentAt = DateTime.UtcNow;
        foreach (var line in pendingLines)
        {
            line.SentToKitchenAtUtc = sentAt;
            _lines.Update(line);
        }

        if (order.Status == SalesOrderStatus.Draft)
            order.Status = SalesOrderStatus.Open;
        RecalculateOrderTotals(order);
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var ticketModel = await _kitchenTickets.BuildTicketModelAsync(order, batchDtos, cancellationToken);
        var ticketPath = await _kitchenTickets.GeneratePdfAsync(ticketModel, cancellationToken);

        var orderDto = await GetByIdAsync(orderId, cancellationToken);
        if (orderDto is null)
            return null;

        return new ConfirmSalesOrderResultDto
        {
            Order = orderDto,
            KitchenTicketRelativePath = ticketPath,
        };
    }

    private async Task ApplyLineToOrderAsync(
        SalesOrder order,
        AddSalesOrderLineDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.Quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero.");

        var product = await _products.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive, cancellationToken);

        if (product is null)
            throw new InvalidOperationException("Product was not found or is inactive.");

        var notes = NormalizeNotes(dto.Notes);
        var excludedIds = await NormalizeExcludedIngredientsAsync(
            product.Id,
            product.CompositionType,
            dto.ExcludedIngredientIds,
            cancellationToken);

        if (!HasCustomization(notes, excludedIds))
        {
            var match = FindMergeablePendingLine(order.Lines, product.Id, notes, excludedIds);
            if (match is not null)
            {
                match.Quantity += dto.Quantity;
                match.LineTotal = decimal.Round(match.Quantity * match.UnitPrice, 2, MidpointRounding.AwayFromZero);
                _lines.Update(match);
                return;
            }
        }

        var lineId = Guid.NewGuid();
        var unitPrice = product.UnitPrice;
        var lineTotal = decimal.Round(dto.Quantity * unitPrice, 2, MidpointRounding.AwayFromZero);

        var line = new SalesOrderLine
        {
            Id = lineId,
            SalesOrderId = order.Id,
            ProductId = product.Id,
            Quantity = dto.Quantity,
            UnitPrice = unitPrice,
            LineTotal = lineTotal,
            Notes = notes,
        };

        foreach (var ingredientId in excludedIds)
        {
            line.ExcludedIngredients.Add(
                new SalesOrderLineExcludedIngredient
                {
                    Id = Guid.NewGuid(),
                    SalesOrderLineId = lineId,
                    IngredientId = ingredientId,
                });
        }

        await _lines.AddAsync(line, cancellationToken);
        order.Lines.Add(line);
    }

    public async Task<SalesOrderDto?> CompleteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.Query()
            .Include(o => o.DiningTable)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return null;

        if (order.Status != SalesOrderStatus.Open)
            throw new InvalidOperationException("Only open orders can be completed.");

        if (!await _lines.Query().AnyAsync(l => l.SalesOrderId == orderId, cancellationToken))
            throw new InvalidOperationException("Add at least one item before completing the order.");

        order.Status = SalesOrderStatus.Paid;
        order.ClosedAtUtc = DateTime.UtcNow;
        _orders.Update(order);

        if (order.DiningTableId is { } tableId)
        {
            var table = order.DiningTable ?? await _tables.GetByIdAsync(tableId, cancellationToken);
            if (table is not null && table.Status == ETableStatus.Busy)
            {
                TableStatusTransitions.EnsureCanTransition(table.Status, ETableStatus.Available);
                table.Status = ETableStatus.Available;
                _tables.Update(table);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(orderId, cancellationToken);
    }

    private async Task<SalesOrderDto?> LoadOrderDtoAsync(
        System.Linq.Expressions.Expression<Func<SalesOrder, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var entity = await OrderWithLinesQuery(tracked: false)
            .Where(predicate)
            .OrderByDescending(o => o.OpenedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
            return null;

        NormalizeOrderLinesAndTotals(entity);
        return _mapper.Map<SalesOrderDto>(entity);
    }

    private IQueryable<SalesOrder> OrderWithLinesQuery(bool tracked)
    {
        var query = tracked ? _orders.Query() : _orders.Query().AsNoTracking();
        return query
            .AsSplitQuery()
            .Include(o => o.DiningTable)
            .Include(o => o.Lines)
            .ThenInclude(l => l.Product)
            .Include(o => o.Lines)
            .ThenInclude(l => l.ExcludedIngredients)
            .ThenInclude(e => e.Ingredient);
    }

    /// <summary>
    /// EF can duplicate line rows when multiple Include paths touch Lines; dedupe before summing.
    /// </summary>
    private static void NormalizeOrderLinesAndTotals(SalesOrder order)
    {
        order.Lines = DistinctLines(order.Lines).ToList();
        order.Subtotal = order.Lines.Sum(l => l.LineTotal);
        order.TaxAmount = 0;
        order.Total = order.Subtotal;
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var suffix = Random.Shared.Next(1000, 9999);
            var number = $"T-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
            if (!await _orders.Query().AnyAsync(o => o.Number == number, cancellationToken))
                return number;
        }

        return $"T-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    }

    private async Task<List<Guid>> NormalizeExcludedIngredientsAsync(
        Guid productId,
        EProductType compositionType,
        List<Guid> requested,
        CancellationToken cancellationToken)
    {
        if (compositionType != EProductType.Prepared || requested.Count == 0)
            return [];

        var recipeIds = await _productIngredients.Query()
            .AsNoTracking()
            .Where(pi => pi.ProductId == productId)
            .Select(pi => pi.IngredientId)
            .ToListAsync(cancellationToken);

        var recipeSet = recipeIds.ToHashSet();
        var distinct = requested.Distinct().ToList();
        if (distinct.Any(id => !recipeSet.Contains(id)))
            throw new InvalidOperationException("Excluded ingredients must belong to the product recipe.");

        return distinct.OrderBy(id => id).ToList();
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;
        return notes.Trim();
    }

    private static bool HasCustomization(string? notes, IReadOnlyList<Guid> excludedIds) =>
        !string.IsNullOrEmpty(notes) || excludedIds.Count > 0;

    private static IEnumerable<SalesOrderLine> DistinctLines(IEnumerable<SalesOrderLine> lines) =>
        lines.GroupBy(l => l.Id).Select(g => g.First());

    private static SalesOrderLine? FindMergeablePendingLine(
        IEnumerable<SalesOrderLine> lines,
        Guid productId,
        string? notes,
        IReadOnlyList<Guid> excludedIds)
    {
        foreach (var line in DistinctLines(lines).Where(l => l.SentToKitchenAtUtc is null))
        {
            if (line.ProductId != productId)
                continue;

            if (!string.Equals(line.Notes, notes, StringComparison.Ordinal))
                continue;

            var lineExcluded = line.ExcludedIngredients.Select(e => e.IngredientId).OrderBy(id => id).ToList();
            if (lineExcluded.Count != excludedIds.Count)
                continue;

            if (lineExcluded.Zip(excludedIds).All(pair => pair.First == pair.Second))
                return line;
        }

        return null;
    }

    private static void RecalculateOrderTotals(SalesOrder order)
    {
        order.Lines = DistinctLines(order.Lines).ToList();
        order.Subtotal = order.Lines.Sum(l => l.LineTotal);
        order.TaxAmount = 0;
        order.Total = order.Subtotal;
    }

    private async Task EnsureSalonOperationsAllowedAsync(CancellationToken cancellationToken)
    {
        var day = await _operationalDay.ResolveAsync(cancellationToken);
        if (day.ClosureStatus == DailyClosureStatus.Closed)
            throw new InvalidOperationException(
                "El día operativo está cerrado. Tras el cierre diario, el sistema pasa al siguiente día operativo: abra un turno de caja antes de tomar pedidos o cobrar.");
    }
}
