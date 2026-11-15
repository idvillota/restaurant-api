using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Reports;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class OperationalReportsService : IOperationalReportsService
{
    private const int MaxRows = 15_000;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenant;

    public OperationalReportsService(ApplicationDbContext db, ICurrentTenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SalesReportDto> GetSalesReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? productId,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(startDate, endDate);
        var tenantId = ResolveTenantId();
        var tenantName = await GetTenantNameAsync(tenantId, cancellationToken);
        var (startUtc, endExclusive) = ToUtcRange(startDate, endDate);

        string? productName = null;
        if (productId.HasValue)
        {
            productName = await _db.Products.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.Id == productId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var query = _db.SalesOrderLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId
                && l.CreatedAtUtc >= startUtc
                && l.CreatedAtUtc < endExclusive
                && l.SalesOrder.Status == SalesOrderStatus.Paid);

        if (productId.HasValue)
            query = query.Where(l => l.ProductId == productId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new SalesReportRowDto
            {
                ProductName = l.Product.Name,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                SoldAtUtc = l.CreatedAtUtc,
            })
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        return new SalesReportDto
        {
            TenantName = tenantName,
            StartDate = startDate,
            EndDate = endDate,
            ProductName = productName,
            TotalCount = totalCount,
            IsTruncated = totalCount > rows.Count,
            Rows = rows,
        };
    }

    public async Task<IngredientsReportDto> GetIngredientsReportAsync(
        string? nameFilter,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var tenantName = await GetTenantNameAsync(tenantId, cancellationToken);
        var trimmed = nameFilter?.Trim();

        var query = _db.Ingredients
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.IsActive);

        if (!string.IsNullOrWhiteSpace(trimmed))
            query = query.Where(i => EF.Functions.ILike(i.Name, $"%{trimmed}%"));

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(i => i.Name)
            .Select(i => new IngredientsReportRowDto
            {
                IngredientName = i.Name,
                UnitCost = i.UnitCost,
                Unit = i.Unit.ToString(),
                StockQuantity = i.StockQuantity,
                ReorderLevel = i.ReorderLevel,
            })
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        return new IngredientsReportDto
        {
            TenantName = tenantName,
            NameFilter = trimmed,
            TotalCount = totalCount,
            IsTruncated = totalCount > rows.Count,
            Rows = rows,
        };
    }

    public async Task<PurchasesReportDto> GetPurchasesReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? ingredientId,
        Guid? providerId,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(startDate, endDate);
        var tenantId = ResolveTenantId();
        var tenantName = await GetTenantNameAsync(tenantId, cancellationToken);
        var (startUtc, endExclusive) = ToUtcRange(startDate, endDate);

        string? ingredientName = null;
        if (ingredientId.HasValue)
        {
            ingredientName = await _db.Ingredients.AsNoTracking()
                .Where(i => i.TenantId == tenantId && i.Id == ingredientId.Value)
                .Select(i => i.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        string? providerName = null;
        if (providerId.HasValue)
        {
            providerName = await _db.Providers.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.Id == providerId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var query = _db.PurchaseLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId
                && l.Purchase.PurchasedAtUtc >= startUtc
                && l.Purchase.PurchasedAtUtc < endExclusive);

        if (ingredientId.HasValue)
            query = query.Where(l => l.IngredientId == ingredientId.Value);
        if (providerId.HasValue)
            query = query.Where(l => l.Purchase.ProviderId == providerId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(l => l.Purchase.PurchasedAtUtc)
            .ThenBy(l => l.Ingredient.Name)
            .Select(l => new PurchasesReportRowDto
            {
                PurchasedAtUtc = l.Purchase.PurchasedAtUtc,
                BillNumber = l.Purchase.BillNumber,
                ProviderName = l.Purchase.Provider.Name,
                IngredientName = l.Ingredient.Name,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
            })
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        return new PurchasesReportDto
        {
            TenantName = tenantName,
            StartDate = startDate,
            EndDate = endDate,
            IngredientName = ingredientName,
            ProviderName = providerName,
            TotalCount = totalCount,
            IsTruncated = totalCount > rows.Count,
            Rows = rows,
        };
    }

    public async Task<CashOutflowsReportDto> GetCashOutflowsReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        CashMovementType? movementType,
        Guid? providerId,
        bool? isCashDrawerPayment,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(startDate, endDate);
        if (movementType is { } type && !CashMovementKinds.IsOutflow(type))
            throw new InvalidOperationException("El tipo seleccionado no es un egreso.");

        var tenantId = ResolveTenantId();
        var tenantName = await GetTenantNameAsync(tenantId, cancellationToken);

        string? movementTypeName = movementType is { } selectedType
            ? CashOutflowTypeLabel(selectedType)
            : null;

        string? providerName = null;
        if (providerId.HasValue)
        {
            providerName = await _db.Providers.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.Id == providerId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var query = _db.CashMovements
            .AsNoTracking()
            .Where(m =>
                m.TenantId == tenantId
                && m.BusinessDate >= startDate
                && m.BusinessDate <= endDate
                && CashMovementKinds.OutflowTypes.Contains(m.MovementType));

        if (movementType.HasValue)
            query = query.Where(m => m.MovementType == movementType.Value);
        if (providerId.HasValue)
            query = query.Where(m => m.ProviderId == providerId.Value);
        if (isCashDrawerPayment.HasValue)
            query = query.Where(m => m.IsCashDrawerPayment == isCashDrawerPayment.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var activeQuery = query.Where(m => m.VoidedAtUtc == null);
        var totalAmount = await activeQuery.SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;
        var totalCashDrawerAmount = await activeQuery
            .Where(m => m.IsCashDrawerPayment)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var rows = await query
            .OrderByDescending(m => m.BusinessDate)
            .ThenByDescending(m => m.OccurredAtUtc)
            .Select(m => new CashOutflowsReportRowDto
            {
                Id = m.Id,
                BusinessDate = m.BusinessDate,
                OccurredAtUtc = m.OccurredAtUtc,
                MovementType = m.MovementType,
                Amount = m.Amount,
                Description = m.Description,
                ProviderName = m.Provider != null ? m.Provider.Name : null,
                IsCashDrawerPayment = m.IsCashDrawerPayment,
                IsVoided = m.VoidedAtUtc != null,
                CashierEmail = m.CreatedByUser.Email,
            })
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        return new CashOutflowsReportDto
        {
            TenantName = tenantName,
            StartDate = startDate,
            EndDate = endDate,
            MovementTypeName = movementTypeName,
            ProviderName = providerName,
            IsCashDrawerPayment = isCashDrawerPayment,
            TotalCount = totalCount,
            IsTruncated = totalCount > rows.Count,
            TotalAmount = totalAmount,
            TotalCashDrawerAmount = totalCashDrawerAmount,
            Rows = rows,
        };
    }

    public async Task<DailySummaryReportDto> GetDailySummaryReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(startDate, endDate);
        var tenantId = ResolveTenantId();
        var tenantName = await GetTenantNameAsync(tenantId, cancellationToken);
        var (startUtc, endExclusive) = ToUtcRange(startDate, endDate);

        var salesByDate = await _db.SalesOrderLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId
                && l.CreatedAtUtc >= startUtc
                && l.CreatedAtUtc < endExclusive
                && l.SalesOrder.Status == SalesOrderStatus.Paid)
            .GroupBy(l => DateOnly.FromDateTime(l.CreatedAtUtc))
            .Select(g => new DailySummarySalesBucket
            {
                Date = g.Key,
                TotalSales = g.Sum(x => x.LineTotal),
                SalesOrderCount = g.Select(x => x.SalesOrderId).Distinct().Count(),
                ItemsSold = g.Sum(x => x.Quantity),
            })
            .ToDictionaryAsync(x => x.Date, cancellationToken);

        var purchasesByDate = await _db.Purchases
            .AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId
                && p.PurchasedAtUtc >= startUtc
                && p.PurchasedAtUtc < endExclusive)
            .GroupBy(p => DateOnly.FromDateTime(p.PurchasedAtUtc))
            .Select(g => new DailySummaryPurchasesBucket
            {
                Date = g.Key,
                TotalPurchases = g.Sum(x => x.Total),
                PurchaseCount = g.Count(),
            })
            .ToDictionaryAsync(x => x.Date, cancellationToken);

        var cashMovementsInRange = await _db.CashMovements
            .AsNoTracking()
            .Where(m =>
                m.TenantId == tenantId
                && m.BusinessDate >= startDate
                && m.BusinessDate <= endDate)
            .Select(m => new { m.BusinessDate, m.Amount, m.MovementType, m.IsCashDrawerPayment, m.VoidedAtUtc })
            .ToListAsync(cancellationToken);

        var outflowsByDate = cashMovementsInRange
            .Where(m =>
                CashMovementKinds.IsOutflow(m.MovementType)
                && m.IsCashDrawerPayment
                && m.VoidedAtUtc is null)
            .GroupBy(m => m.BusinessDate)
            .ToDictionary(
                g => g.Key,
                g => new DailySummaryOutflowsBucket
                {
                    Date = g.Key,
                    TotalCashOutflows = g.Sum(x => x.Amount),
                    CashOutflowCount = g.Count(),
                });

        var inflowsByDate = cashMovementsInRange
            .Where(m =>
                CashMovementKinds.IsInflow(m.MovementType)
                && m.IsCashDrawerPayment
                && m.VoidedAtUtc is null)
            .GroupBy(m => m.BusinessDate)
            .ToDictionary(
                g => g.Key,
                g => new DailySummaryInflowsBucket
                {
                    Date = g.Key,
                    TotalCashInflows = g.Sum(x => x.Amount),
                    CashInflowCount = g.Count(),
                });

        var dates = salesByDate.Keys
            .Union(purchasesByDate.Keys)
            .Union(outflowsByDate.Keys)
            .Union(inflowsByDate.Keys)
            .OrderDescending()
            .ToList();
        var rows = dates.Select(date =>
        {
            salesByDate.TryGetValue(date, out var sales);
            purchasesByDate.TryGetValue(date, out var purchases);
            outflowsByDate.TryGetValue(date, out var outflows);
            inflowsByDate.TryGetValue(date, out var inflows);
            var totalSales = sales?.TotalSales ?? 0m;
            var totalPurchases = purchases?.TotalPurchases ?? 0m;
            var totalOutflows = outflows?.TotalCashOutflows ?? 0m;
            var totalInflows = inflows?.TotalCashInflows ?? 0m;

            return new DailySummaryReportRowDto
            {
                Date = date,
                TotalSales = totalSales,
                TotalPurchases = totalPurchases,
                TotalCashOutflows = totalOutflows,
                TotalCashInflows = totalInflows,
                NetResult = totalSales - totalPurchases - totalOutflows + totalInflows,
                SalesOrderCount = sales?.SalesOrderCount ?? 0,
                PurchaseCount = purchases?.PurchaseCount ?? 0,
                CashOutflowCount = outflows?.CashOutflowCount ?? 0,
                CashInflowCount = inflows?.CashInflowCount ?? 0,
                ItemsSold = sales?.ItemsSold ?? 0m,
            };
        }).ToList();

        return new DailySummaryReportDto
        {
            TenantName = tenantName,
            StartDate = startDate,
            EndDate = endDate,
            GrandTotalSales = rows.Sum(r => r.TotalSales),
            GrandTotalPurchases = rows.Sum(r => r.TotalPurchases),
            GrandTotalCashOutflows = rows.Sum(r => r.TotalCashOutflows),
            GrandTotalCashInflows = rows.Sum(r => r.TotalCashInflows),
            GrandNetResult = rows.Sum(r => r.NetResult),
            Rows = rows,
        };
    }

    private sealed class DailySummarySalesBucket
    {
        public DateOnly Date { get; init; }
        public decimal TotalSales { get; init; }
        public int SalesOrderCount { get; init; }
        public decimal ItemsSold { get; init; }
    }

    private sealed class DailySummaryPurchasesBucket
    {
        public DateOnly Date { get; init; }
        public decimal TotalPurchases { get; init; }
        public int PurchaseCount { get; init; }
    }

    private sealed class DailySummaryOutflowsBucket
    {
        public DateOnly Date { get; init; }
        public decimal TotalCashOutflows { get; init; }
        public int CashOutflowCount { get; init; }
    }

    private sealed class DailySummaryInflowsBucket
    {
        public DateOnly Date { get; init; }
        public decimal TotalCashInflows { get; init; }
        public int CashInflowCount { get; init; }
    }

    private async Task<string> GetTenantNameAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var name = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return name ?? string.Empty;
    }

    private static (DateTime StartUtc, DateTime EndExclusive) ToUtcRange(DateOnly startDate, DateOnly endDate)
    {
        var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return (startUtc, endExclusive);
    }

    private static string CashOutflowTypeLabel(CashMovementType type) =>
        type switch
        {
            CashMovementType.ProviderPayment => "Pago a proveedor",
            CashMovementType.PettyCash => "Caja menor",
            CashMovementType.Refund => "Devolución",
            CashMovementType.OtherOutflow => "Otro egreso",
            CashMovementType.AdjustmentOut => "Ajuste salida",
            _ => type.ToString(),
        };

    private static void ValidateDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            throw new InvalidOperationException("La fecha final debe ser igual o posterior a la fecha inicial.");

        if (endDate.DayNumber - startDate.DayNumber > 366)
            throw new InvalidOperationException("El rango de fechas no puede superar 366 días.");
    }

    private Guid ResolveTenantId()
    {
        if (_tenant.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }
}
