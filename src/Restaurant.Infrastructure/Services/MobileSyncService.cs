using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.MobileSync;
using Restaurant.Application.Features.Sales.Bills;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Infrastructure.KitchenTickets;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class MobileSyncService : IMobileSyncService
{
    private readonly ApplicationDbContext _db;
    private readonly ISalesOrderService _orders;
    private readonly IMobileSalePublisher _publisher;
    private readonly IBillService _bills;
    private readonly IPrintJobQueue? _printJobs;
    private readonly IKitchenTicketService? _kitchenTickets;

    public MobileSyncService(
        ApplicationDbContext db,
        ISalesOrderService orders,
        IMobileSalePublisher publisher,
        IBillService bills,
        IPrintJobQueue? printJobs = null,
        IKitchenTicketService? kitchenTickets = null)
    {
        _db = db;
        _orders = orders;
        _publisher = publisher;
        _bills = bills;
        _printJobs = printJobs;
        _kitchenTickets = kitchenTickets;
    }

    public async Task<MobileSyncBatchResponse> UploadBatchAsync(
        MobileSyncBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MobileSyncBatchResponse();
        foreach (var order in request.Orders)
        {
            try
            {
                response.Results.Add(await UploadOneAsync(request.DeviceId, order, cancellationToken));
            }
            catch (Exception ex)
            {
                response.Results.Add(new MobileSyncBatchResult
                {
                    LocalId = order.LocalId,
                    Status = "failed",
                    Error = ex.Message,
                });
            }
        }

        return response;
    }

    public async Task<MobileSyncPaymentResponse> PayAsync(
        MobileSyncPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.TipAmount < 0)
                return PaymentFailed("La propina no es válida.");

            var ids = request.RemoteIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (ids.Count == 0)
                return PaymentFailed("La cuenta no tiene pedidos en HOST.");

            var requested = await _db.SalesOrders
                .AsNoTracking()
                .Where(order => ids.Contains(order.Id))
                .Select(order => new { order.Id, order.DiningTableId, order.Status })
                .ToListAsync(cancellationToken);

            if (requested.Count != ids.Count)
                return PaymentFailed("No se encontró el pedido en HOST.");

            var tableIds = requested
                .Where(order => order.DiningTableId != null)
                .Select(order => order.DiningTableId!.Value)
                .Distinct()
                .ToList();

            var settling = requested.Any(order =>
                order.Status is SalesOrderStatus.Draft or SalesOrderStatus.Open);
            var openIds = settling
                ? await OpenOrderIdsAsync(tableIds, ids, cancellationToken)
                : new List<Guid>();

            if (openIds.Count > 0)
            {
                var preview = await _bills.PreviewCheckoutAsync(
                    new CheckoutPreviewDto
                    {
                        SalesOrderIds = openIds,
                        TipAmount = request.TipAmount,
                    },
                    cancellationToken);

                await _bills.FinalizeCheckoutAsync(
                    new FinalizeCheckoutDto
                    {
                        SalesOrderIds = openIds,
                        TipAmount = request.TipAmount,
                        Payments =
                        [
                            new CheckoutPaymentLineDto
                            {
                                Amount = preview.TotalDue,
                                Method = MapPaymentMethod(request.PaymentMethod),
                            },
                        ],
                    },
                    cancellationToken);
            }

            await ReleaseIdleTablesAsync(tableIds, cancellationToken);
            var stillBusy = tableIds.Count > 0 && await _db.DiningTables
                .AsNoTracking()
                .AnyAsync(
                    table => tableIds.Contains(table.Id) && table.Status == ETableStatus.Busy,
                    cancellationToken);

            return new MobileSyncPaymentResponse
            {
                Status = "synced",
                TablesAvailable = !stillBusy,
            };
        }
        catch (InvalidOperationException ex)
        {
            return PaymentFailed(MapPaymentError(ex.Message));
        }
    }

    public async Task<PendingMobileSalesResponse> GetPendingSalesAsync(
        string deviceId,
        string? since,
        CancellationToken cancellationToken = default)
    {
        DateTime? sinceUtc = DateTime.TryParse(
            since,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;

        var orders = await _db.SalesOrders
            .AsNoTracking()
            .Include(order => order.DiningTable)
            .Include(order => order.Lines)
            .ThenInclude(line => line.Product)
            .Where(order => order.Status == SalesOrderStatus.Draft || order.Status == SalesOrderStatus.Open)
            .ToListAsync(cancellationToken);

        if (sinceUtc is not null)
        {
            orders = orders
                .Where(order => Stamp(order) > sinceUtc.Value)
                .ToList();
        }

        var ownedLineIds = await OwnedLineIdsAsync(deviceId, cancellationToken);
        var sales = new List<PendingMobileSale>();
        foreach (var order in orders)
        {
            var lines = order.Lines
                .Where(line => line.Quantity > 0 && !ownedLineIds.Contains(line.Id))
                .Select(line => new PendingMobileSaleLine
                {
                    LineId = line.Id,
                    ProductId = line.ProductId,
                    ProductName = line.Product?.Name ?? "Producto",
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    Notes = line.Notes ?? string.Empty,
                })
                .ToList();

            if (lines.Count == 0)
                continue;

            sales.Add(new PendingMobileSale
            {
                RemoteOrderId = order.Id,
                TableId = order.DiningTableId,
                TableCode = order.DiningTable?.Code ?? string.Empty,
                Cursor = Stamp(order).ToString("O"),
                Lines = lines,
            });
        }

        var cursor = orders.Count == 0
            ? since ?? string.Empty
            : orders.Max(Stamp).ToString("O");

        return new PendingMobileSalesResponse
        {
            Sales = sales,
            Cursor = cursor,
        };
    }

    private async Task<MobileSyncBatchResult> UploadOneAsync(
        string deviceId,
        MobileSyncBatchOrder request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LocalId))
            throw new InvalidOperationException("La comanda no tiene id local.");

        var existing = await _db.MobileSyncReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                receipt => receipt.DeviceId == deviceId && receipt.LocalOrderId == request.LocalId,
                cancellationToken);
        if (existing is not null)
        {
            return new MobileSyncBatchResult
            {
                LocalId = request.LocalId,
                RemoteId = existing.RemoteOrderId.ToString(),
                Status = "synced",
            };
        }

        var prepared = await PrepareLinesAsync(request.Lines, cancellationToken);
        var order = await ResolveOrderAsync(request, cancellationToken);
        var sentAt = DateTime.UtcNow;
        var createdIds = new List<Guid>();
        decimal addedTotal = 0;

        foreach (var (product, quantity, notes) in prepared)
        {
            var lineId = Guid.NewGuid();
            var lineTotal = decimal.Round(quantity * product.UnitPrice, 2, MidpointRounding.AwayFromZero);
            _db.SalesOrderLines.Add(new SalesOrderLine
            {
                Id = lineId,
                SalesOrderId = order.Id,
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = product.UnitPrice,
                LineTotal = lineTotal,
                Notes = notes,
                SentToKitchenAtUtc = sentAt,
            });
            createdIds.Add(lineId);
            addedTotal += lineTotal;
        }

        var storedTotals = await _db.SalesOrderLines
            .Where(line => line.SalesOrderId == order.Id && line.Quantity > 0)
            .Select(line => line.LineTotal)
            .ToListAsync(cancellationToken);
        order.Subtotal = storedTotals.Sum() + addedTotal;
        order.TaxAmount = 0;
        order.Total = order.Subtotal;
        if (order.Status == SalesOrderStatus.Draft)
            order.Status = SalesOrderStatus.Open;
        if (order.DiningTable is { } table && table.Status != ETableStatus.Busy)
            table.Status = ETableStatus.Busy;

        _db.MobileSyncReceipts.Add(new MobileSyncReceipt
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            LocalOrderId = request.LocalId,
            RemoteOrderId = order.Id,
            LineIds = string.Join(',', createdIds),
        });
        await _db.SaveChangesAsync(cancellationToken);

        var cursor = Stamp(order).ToString("O");
        try
        {
            await _publisher.PublishAsync(
            order.TenantId,
            new MobileSaleNotice(
                order.Id,
                order.DiningTableId,
                order.DiningTable?.Code ?? request.TableCode ?? string.Empty,
                string.IsNullOrWhiteSpace(request.WaiterName) ? "HOST" : request.WaiterName.Trim(),
                cursor,
                prepared.Zip(createdIds, (line, lineId) => new MobileSaleLineNotice(
                    lineId,
                    line.Product.Id,
                    line.Product.Name,
                    line.Quantity,
                    line.Product.UnitPrice,
                    line.Notes)).ToList()),
            deviceId,
            cancellationToken);
        }
        catch (Exception)
        {
            // The lines are already stored. Another tablet can catch up with pending-sales.
        }

        await EnqueueAddedKitchenTicketAsync(order, request, prepared, deviceId, cancellationToken);

        return new MobileSyncBatchResult
        {
            LocalId = request.LocalId,
            RemoteId = order.Id.ToString(),
            Status = "synced",
        };
    }

    private async Task EnqueueAddedKitchenTicketAsync(
        SalesOrder order,
        MobileSyncBatchOrder request,
        IReadOnlyList<(Product Product, decimal Quantity, string? Notes)> prepared,
        string deviceId,
        CancellationToken cancellationToken)
    {
        if (_printJobs is null || _kitchenTickets is null || prepared.Count == 0)
            return;

        try
        {
            var batch = prepared.Select(line => new AddSalesOrderLineDto
            {
                ProductId = line.Product.Id,
                Quantity = line.Quantity,
                Notes = line.Notes,
            }).ToList();
            var model = await _kitchenTickets.BuildTicketModelAsync(order, batch, cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.WaiterName))
                model.SentBy = request.WaiterName.Trim();
            if (model.Lines.Count == 0)
                return;

            await _printJobs.EnqueueAsync(
                PrintJobKinds.Kitchen,
                PrintJobFormats.KitchenTicketXml,
                KitchenTicketPrintXml.Write(model),
                cancellationToken,
                deviceId);
        }
        catch (Exception)
        {
            // The lines are already stored. A print failure must not undo the comanda.
        }
    }

    private async Task<List<(Product Product, decimal Quantity, string? Notes)>> PrepareLinesAsync(
        IReadOnlyList<MobileSyncBatchLine> lines,
        CancellationToken cancellationToken)
    {
        var prepared = new List<(Product Product, decimal Quantity, string? Notes)>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                continue;

            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == line.ProductId && item.IsActive, cancellationToken);
            if (product is null)
                throw new InvalidOperationException("Producto no encontrado o inactivo.");

            var notes = string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim();
            if (notes is { Length: > 500 })
                notes = notes[..500];
            prepared.Add((product, line.Quantity, notes));
        }

        if (prepared.Count == 0)
            throw new InvalidOperationException("La comanda no tiene productos.");

        return prepared;
    }

    private async Task<SalesOrder> ResolveOrderAsync(
        MobileSyncBatchOrder request,
        CancellationToken cancellationToken)
    {
        if (request.RemoteId is Guid remoteId)
        {
            var byId = await OpenOrderQuery()
                .FirstOrDefaultAsync(order => order.Id == remoteId, cancellationToken);
            if (byId is not null)
                return byId;

            var closed = await _db.SalesOrders
                .AsNoTracking()
                .AnyAsync(order => order.Id == remoteId, cancellationToken);
            if (closed)
                throw new InvalidOperationException("El pedido ya está cerrado.");
        }

        if (!Guid.TryParse(request.TableId, out var tableId))
            throw new InvalidOperationException("La comanda no tiene mesa.");

        var byTable = await OpenOrderQuery()
            .FirstOrDefaultAsync(order => order.DiningTableId == tableId, cancellationToken);
        if (byTable is not null)
            return byTable;

        try
        {
            var created = await _orders.StartOrderForTableAsync(tableId, cancellationToken);
            return await OpenOrderQuery().FirstAsync(order => order.Id == created.Id, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already has an active order", StringComparison.Ordinal))
        {
            return await OpenOrderQuery().FirstAsync(order => order.DiningTableId == tableId, cancellationToken);
        }
    }

    private IQueryable<SalesOrder> OpenOrderQuery() =>
        _db.SalesOrders
            .Include(order => order.DiningTable)
            .Where(order => order.Status == SalesOrderStatus.Draft || order.Status == SalesOrderStatus.Open);

    private async Task<HashSet<Guid>> OwnedLineIdsAsync(string deviceId, CancellationToken cancellationToken)
    {
        var stored = await _db.MobileSyncReceipts
            .AsNoTracking()
            .Where(receipt => receipt.DeviceId == deviceId)
            .Select(receipt => receipt.LineIds)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<Guid>();
        foreach (var value in stored)
        {
            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(part, out var id))
                    ids.Add(id);
            }
        }

        return ids;
    }

    private static DateTime Stamp(SalesOrder order) =>
        order.UpdatedAtUtc ?? order.CreatedAtUtc;

    private async Task<List<Guid>> OpenOrderIdsAsync(
        IReadOnlyCollection<Guid> tableIds,
        IReadOnlyCollection<Guid> requestedIds,
        CancellationToken cancellationToken)
    {
        var onTables = tableIds.Count == 0
            ? new List<Guid>()
            : await _db.SalesOrders
                .AsNoTracking()
                .Where(order =>
                    order.DiningTableId != null &&
                    tableIds.Contains(order.DiningTableId.Value) &&
                    (order.Status == SalesOrderStatus.Draft || order.Status == SalesOrderStatus.Open))
                .Select(order => order.Id)
                .ToListAsync(cancellationToken);

        var requestedOpen = await _db.SalesOrders
            .AsNoTracking()
            .Where(order =>
                requestedIds.Contains(order.Id) &&
                (order.Status == SalesOrderStatus.Draft || order.Status == SalesOrderStatus.Open))
            .Select(order => order.Id)
            .ToListAsync(cancellationToken);

        return onTables.Concat(requestedOpen).Distinct().ToList();
    }

    private async Task ReleaseIdleTablesAsync(
        IReadOnlyCollection<Guid> tableIds,
        CancellationToken cancellationToken)
    {
        if (tableIds.Count == 0)
            return;

        var stillOpen = await _db.SalesOrders
            .AsNoTracking()
            .Where(order =>
                order.DiningTableId != null &&
                tableIds.Contains(order.DiningTableId.Value) &&
                (order.Status == SalesOrderStatus.Draft || order.Status == SalesOrderStatus.Open))
            .Select(order => order.DiningTableId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var idleIds = tableIds.Except(stillOpen).ToList();
        if (idleIds.Count == 0)
            return;

        var tables = await _db.DiningTables
            .Where(table => idleIds.Contains(table.Id) && table.IsActive && table.Status == ETableStatus.Busy)
            .ToListAsync(cancellationToken);

        foreach (var table in tables)
        {
            if (!TableStatusTransitions.CanTransition(table.Status, ETableStatus.Available))
                continue;
            table.Status = ETableStatus.Available;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static MobileSyncPaymentResponse PaymentFailed(string error) =>
        new() { Status = "failed", Error = error };

    private static PaymentMethod MapPaymentMethod(string? method) =>
        method?.Trim().ToLowerInvariant() switch
        {
            "tarjeta" or "card" => PaymentMethod.Card,
            "transferencia" or "transfer" => PaymentMethod.Transfer,
            "otro" or "other" => PaymentMethod.Other,
            _ => PaymentMethod.Cash,
        };

    private static string MapPaymentError(string message) =>
        message.Contains("cashier shift", StringComparison.OrdinalIgnoreCase)
            ? "Abre un turno de caja en HOST antes de cobrar."
            : message;
}
