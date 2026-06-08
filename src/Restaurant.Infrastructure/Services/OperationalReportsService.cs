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

        var rows = await query
            .OrderBy(i => i.Name)
            .Select(i => new IngredientsReportRowDto
            {
                IngredientName = i.Name,
                UnitCost = i.UnitCost,
                Unit = i.Unit.ToString(),
                StockQuantity = i.StockQuantity,
            })
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        return new IngredientsReportDto
        {
            TenantName = tenantName,
            NameFilter = trimmed,
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

        var salesFacts = await _db.SalesOrderLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId
                && l.CreatedAtUtc >= startUtc
                && l.CreatedAtUtc < endExclusive
                && l.SalesOrder.Status == SalesOrderStatus.Paid)
            .Select(l => new
            {
                Date = DateOnly.FromDateTime(l.CreatedAtUtc),
                l.SalesOrderId,
                l.LineTotal,
                l.Quantity,
            })
            .ToListAsync(cancellationToken);

        var salesByDate = salesFacts
            .GroupBy(x => x.Date)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    TotalSales = g.Sum(x => x.LineTotal),
                    SalesOrderCount = g.Select(x => x.SalesOrderId).Distinct().Count(),
                    ItemsSold = g.Sum(x => x.Quantity),
                });

        var purchaseFacts = await _db.Purchases
            .AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId
                && p.PurchasedAtUtc >= startUtc
                && p.PurchasedAtUtc < endExclusive)
            .Select(p => new
            {
                Date = DateOnly.FromDateTime(p.PurchasedAtUtc),
                p.Total,
            })
            .ToListAsync(cancellationToken);

        var purchasesByDate = purchaseFacts
            .GroupBy(x => x.Date)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    TotalPurchases = g.Sum(x => x.Total),
                    PurchaseCount = g.Count(),
                });

        var dates = salesByDate.Keys.Union(purchasesByDate.Keys).OrderDescending().ToList();
        var rows = dates.Select(date =>
        {
            salesByDate.TryGetValue(date, out var sales);
            purchasesByDate.TryGetValue(date, out var purchases);
            var totalSales = sales?.TotalSales ?? 0m;
            var totalPurchases = purchases?.TotalPurchases ?? 0m;

            return new DailySummaryReportRowDto
            {
                Date = date,
                TotalSales = totalSales,
                TotalPurchases = totalPurchases,
                NetResult = totalSales - totalPurchases,
                SalesOrderCount = sales?.SalesOrderCount ?? 0,
                PurchaseCount = purchases?.PurchaseCount ?? 0,
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
            GrandNetResult = rows.Sum(r => r.NetResult),
            Rows = rows,
        };
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
