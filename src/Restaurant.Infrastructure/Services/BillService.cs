using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.Bills;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class BillService : IBillService
{
    public const string DefaultCustomerTaxId = "2222222";
    public const string DefaultCustomerName = "Consumidor final";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryAvailabilityService _inventory;
    private readonly ICashierShiftService _cashierShifts;

    public BillService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork,
        IInventoryAvailabilityService inventory,
        ICashierShiftService cashierShifts)
    {
        _db = db;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
        _inventory = inventory;
        _cashierShifts = cashierShifts;
    }

    public async Task<IReadOnlyList<PayableTableGroupDto>> ListPayableByTableSearchAsync(
        string? tableSearch,
        CancellationToken cancellationToken = default)
    {
        var search = tableSearch?.Trim();
        var orders = await LoadOpenOrdersQuery()
            .Where(o =>
                search == null ||
                search.Length == 0 ||
                (o.DiningTable != null && EF.Functions.ILike(o.DiningTable.Code, $"%{search}%")))
            .ToListAsync(cancellationToken);

        return orders
            .GroupBy(o => o.DiningTableId)
            .Select(g =>
            {
                var first = g.First();
                return new PayableTableGroupDto
                {
                    TableId = first.DiningTableId ?? Guid.Empty,
                    TableCode = first.DiningTable?.Code ?? "—",
                    Zone = first.DiningTable?.Zone,
                    Orders = g.Select(MapPayableOrder).ToList(),
                };
            })
            .OrderBy(t => t.TableCode)
            .ToList();
    }

    public Task<CheckoutTotalsDto> PreviewCheckoutAsync(
        CheckoutPreviewDto dto,
        CancellationToken cancellationToken = default) =>
        BuildCheckoutTotalsAsync(dto, cancellationToken);

    public async Task<BillDto> FinalizeCheckoutAsync(
        FinalizeCheckoutDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.Payments.Count == 0)
            throw new InvalidOperationException("Add at least one payment.");

        var totals = await BuildCheckoutTotalsAsync(dto, cancellationToken);
        var stockCheck = await _inventory.CheckOrdersForPaymentAsync(dto.SalesOrderIds, cancellationToken);
        _inventory.EnsureAvailable(stockCheck);

        var paymentSum = dto.Payments.Sum(p => p.Amount);
        if (paymentSum < totals.TotalDue)
            throw new InvalidOperationException(
                $"Payments ({paymentSum:N2}) do not cover the total due ({totals.TotalDue:N2}).");

        var orders = await LoadOpenOrdersQuery()
            .Where(o => dto.SalesOrderIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        if (orders.Count != dto.SalesOrderIds.Distinct().Count())
            throw new InvalidOperationException("One or more orders are not open or were not found.");

        var (shiftId, processedByUserId) = await _cashierShifts.RequireOpenShiftAsync(cancellationToken);

        var customer = await ResolveCustomerAsync(dto.CustomerId, cancellationToken);
        var now = DateTime.UtcNow;
        var billId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var billNumber = await GenerateBillNumberAsync(cancellationToken);
        var invoiceNumber = $"INV-{billNumber}";

        var bill = new Bill
        {
            Id = billId,
            CustomerId = customer.Id,
            Number = billNumber,
            Status = BillStatus.Paid,
            Subtotal = totals.Subtotal,
            DiscountAmount = totals.DiscountAmount,
            DiscountPercent = totals.DiscountPercent,
            TipAmount = totals.TipAmount,
            TaxAmount = totals.TaxAmount,
            Total = totals.TotalDue,
            IssuedAtUtc = now,
            PaidAtUtc = now,
            CashierShiftId = shiftId,
            ProcessedByUserId = processedByUserId,
        };

        await _db.Bills.AddAsync(bill, cancellationToken);

        foreach (var order in orders)
        {
            await _db.BillSalesOrders.AddAsync(
                new BillSalesOrder { BillId = billId, SalesOrderId = order.Id },
                cancellationToken);

            order.Status = SalesOrderStatus.Paid;
            order.ClosedAtUtc = now;
            _db.SalesOrders.Update(order);

            await DeductInventoryForOrderAsync(order, cancellationToken);
            await ReleaseTableIfIdleAsync(order.DiningTableId, order.Id, cancellationToken);
        }

        var invoice = new Invoice
        {
            Id = invoiceId,
            BillId = billId,
            CustomerId = customer.Id,
            Number = invoiceNumber,
            Status = InvoiceStatus.Paid,
            Subtotal = totals.Subtotal,
            TaxAmount = totals.TaxAmount,
            Total = totals.TotalDue,
            IssuedAtUtc = now,
        };
        await _db.Invoices.AddAsync(invoice, cancellationToken);

        foreach (var paymentDto in dto.Payments)
        {
            await _db.Payments.AddAsync(
                new Payment
                {
                    Id = Guid.NewGuid(),
                    BillId = billId,
                    InvoiceId = invoiceId,
                    Amount = paymentDto.Amount,
                    Method = paymentDto.Method,
                    Status = PaymentStatus.Completed,
                    ExternalReference = paymentDto.ExternalReference?.Trim(),
                    PaidAtUtc = now,
                    CashierShiftId = shiftId,
                    ProcessedByUserId = processedByUserId,
                },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new BillDto
        {
            Id = billId,
            Number = billNumber,
            Status = BillStatus.Paid,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            Subtotal = totals.Subtotal,
            DiscountAmount = totals.DiscountAmount,
            DiscountPercent = totals.DiscountPercent,
            TipAmount = totals.TipAmount,
            TaxAmount = totals.TaxAmount,
            Total = totals.TotalDue,
            PaidAtUtc = now,
            InvoiceId = invoiceId,
            InvoiceNumber = invoiceNumber,
            SalesOrderIds = orders.Select(o => o.Id).ToList(),
        };
    }

    private async Task<CheckoutTotalsDto> BuildCheckoutTotalsAsync(
        CheckoutPreviewDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.SalesOrderIds.Count == 0)
            throw new InvalidOperationException("Select at least one order.");

        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        var orders = await LoadOpenOrdersQuery()
            .Where(o => dto.SalesOrderIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        if (orders.Count != dto.SalesOrderIds.Distinct().Count())
            throw new InvalidOperationException("One or more orders are not open or were not found.");

        var stockCheck = await _inventory.CheckOrdersForPaymentAsync(dto.SalesOrderIds, cancellationToken);
        _inventory.EnsureAvailable(stockCheck);

        var lines = orders.SelectMany(MapOrderLines).ToList();
        var subtotal = lines.Sum(l => l.LineTotal);
        var (discountAmount, discountPercent) = ComputeDiscount(
            subtotal,
            settings.MaxDiscountPercent,
            dto.DiscountPercent,
            dto.DiscountAmount);
        var tip = Math.Max(0, dto.TipAmount);
        var tax = 0m;
        var totalDue = Math.Max(0, subtotal - discountAmount + tip + tax);

        return new CheckoutTotalsDto
        {
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            DiscountPercent = discountPercent,
            MaxDiscountPercent = settings.MaxDiscountPercent,
            TipAmount = tip,
            TaxAmount = tax,
            TotalDue = decimal.Round(totalDue, 2, MidpointRounding.AwayFromZero),
            Lines = lines,
        };
    }

    internal static (decimal Amount, decimal? Percent) ComputeDiscount(
        decimal subtotal,
        decimal maxDiscountPercent,
        decimal? requestedPercent,
        decimal? requestedAmount)
    {
        if (subtotal <= 0)
            return (0, null);

        var maxAllowed = decimal.Round(
            subtotal * (maxDiscountPercent / 100m),
            2,
            MidpointRounding.AwayFromZero);

        if (requestedPercent is > 0)
        {
            if (requestedPercent > maxDiscountPercent)
                throw new InvalidOperationException(
                    $"Discount percent cannot exceed {maxDiscountPercent:N2}%.");

            var amount = decimal.Round(
                subtotal * (requestedPercent.Value / 100m),
                2,
                MidpointRounding.AwayFromZero);
            return (amount, requestedPercent);
        }

        if (requestedAmount is > 0)
        {
            if (requestedAmount > maxAllowed)
                throw new InvalidOperationException(
                    $"Discount cannot exceed {maxAllowed:N2} ({maxDiscountPercent:N2}% of subtotal).");

            var impliedPercent = decimal.Round(requestedAmount.Value / subtotal * 100m, 2, MidpointRounding.AwayFromZero);
            return (requestedAmount.Value, impliedPercent);
        }

        return (0, null);
    }

    private async Task DeductInventoryForOrderAsync(SalesOrder order, CancellationToken cancellationToken)
    {
        var distinctLines = order.Lines
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .ToList();

        foreach (var line in distinctLines)
        {
            var recipe = await _db.ProductIngredients
                .AsNoTracking()
                .Where(pi => pi.ProductId == line.ProductId)
                .ToListAsync(cancellationToken);

            if (recipe.Count == 0)
                continue;

            var excluded = line.ExcludedIngredients.Select(e => e.IngredientId).ToHashSet();

            foreach (var row in recipe)
            {
                if (excluded.Contains(row.IngredientId))
                    continue;

                var ingredient = await _db.Ingredients.FirstOrDefaultAsync(
                    i => i.Id == row.IngredientId && i.IsActive,
                    cancellationToken);

                if (ingredient is null)
                    continue;

                var deduct = row.Quantity * line.Quantity;
                ingredient.StockQuantity = InventoryCosting.SubtractStock(ingredient.StockQuantity, deduct);
                _db.Ingredients.Update(ingredient);
            }
        }
    }

    private async Task ReleaseTableIfIdleAsync(
        Guid? diningTableId,
        Guid paidOrderId,
        CancellationToken cancellationToken)
    {
        if (diningTableId is null)
            return;

        var hasOtherActive = await _db.SalesOrders.AnyAsync(
            o =>
                o.DiningTableId == diningTableId &&
                o.Id != paidOrderId &&
                (o.Status == SalesOrderStatus.Draft || o.Status == SalesOrderStatus.Open),
            cancellationToken);

        if (hasOtherActive)
            return;

        var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == diningTableId, cancellationToken);
        if (table is null || !table.IsActive)
            return;

        if (table.Status == ETableStatus.Busy)
        {
            TableStatusTransitions.EnsureCanTransition(table.Status, ETableStatus.Available);
            table.Status = ETableStatus.Available;
            _db.DiningTables.Update(table);
        }
    }

    private async Task<Customer> ResolveCustomerAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        if (customerId is { } id)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(
                c => c.Id == id && c.IsActive,
                cancellationToken);
            if (customer is null)
                throw new InvalidOperationException("Customer was not found or is inactive.");
            return customer;
        }

        return await GetOrCreateDefaultCustomerAsync(cancellationToken);
    }

    private async Task<Customer> GetOrCreateDefaultCustomerAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(
            c => c.TaxId == DefaultCustomerTaxId && c.IsActive,
            cancellationToken);

        if (existing is not null)
            return existing;

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = DefaultCustomerName,
            TaxId = DefaultCustomerTaxId,
            IsActive = true,
        };
        await _db.Customers.AddAsync(customer, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private async Task<TenantSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (settings is not null)
            return settings;

        settings = new TenantSettings
        {
            TenantId = tenantId,
            MaxDiscountPercent = 10m,
            OperationalDayCutoffHour = 4,
        };
        await _db.TenantSettings.AddAsync(settings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private IQueryable<SalesOrder> LoadOpenOrdersQuery() =>
        _db.SalesOrders
            .AsSplitQuery()
            .Include(o => o.DiningTable)
            .Include(o => o.Lines)
            .ThenInclude(l => l.Product)
            .Include(o => o.Lines)
            .ThenInclude(l => l.ExcludedIngredients)
            .Where(o => o.Status == SalesOrderStatus.Open && o.Lines.Any());

    private static PayableOrderDto MapPayableOrder(SalesOrder order)
    {
        var lines = order.Lines.GroupBy(l => l.Id).Select(g => g.First()).ToList();
        return new PayableOrderDto
        {
            OrderId = order.Id,
            OrderNumber = order.Number,
            Total = lines.Sum(l => l.LineTotal),
            Lines = lines.Select(l => new PayableOrderLineDto
            {
                LineId = l.Id,
                OrderId = order.Id,
                OrderNumber = order.Number,
                TableCode = order.DiningTable?.Code,
                ProductId = l.ProductId,
                ProductName = l.Product.Name,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                Notes = l.Notes,
            }).ToList(),
        };
    }

    private static IEnumerable<PayableOrderLineDto> MapOrderLines(SalesOrder order) =>
        order.Lines
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .Select(l => new PayableOrderLineDto
            {
                LineId = l.Id,
                OrderId = order.Id,
                OrderNumber = order.Number,
                TableCode = order.DiningTable?.Code,
                ProductId = l.ProductId,
                ProductName = l.Product.Name,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                Notes = l.Notes,
            });

    private async Task<string> GenerateBillNumberAsync(CancellationToken cancellationToken)
    {
        var count = await _db.Bills.CountAsync(cancellationToken);
        return $"BILL-{count + 1:D4}";
    }

    private Guid ResolveTenantId()
    {
        if (_tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }
}
