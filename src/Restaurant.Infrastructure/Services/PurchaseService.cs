using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Procurement.Purchases;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class PurchaseService : IPurchaseService
{
    private readonly IRepository<Purchase> _purchases;
    private readonly IRepository<PurchaseLine> _purchaseLines;
    private readonly IRepository<Provider> _providers;
    private readonly IRepository<Ingredient> _ingredients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public PurchaseService(
        IRepository<Purchase> purchases,
        IRepository<PurchaseLine> purchaseLines,
        IRepository<Provider> providers,
        IRepository<Ingredient> ingredients,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _purchases = purchases;
        _purchaseLines = purchaseLines;
        _providers = providers;
        _ingredients = ingredients;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResult<PurchaseListItemDto>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = query.Normalize();
        var shaped = PagedEntityQueries.ShapePurchases(_purchases.Query().AsNoTracking(), query);
        var projected = shaped.Select(p => new PurchaseListItemDto
        {
            Id = p.Id,
            ProviderId = p.ProviderId,
            ProviderName = p.Provider.Name,
            BillNumber = p.BillNumber,
            PurchasedAtUtc = p.PurchasedAtUtc,
            PaymentDateUtc = p.PaymentDateUtc,
            Subtotal = p.Subtotal,
            TaxAmount = p.TaxAmount,
            Total = p.Total,
            LineCount = p.Lines.Count,
        });

        var totalCount = await projected.CountAsync(cancellationToken);
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<PurchaseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _purchases.Query()
            .AsNoTracking()
            .Include(p => p.Provider)
            .Include(p => p.Lines)
            .ThenInclude(l => l.Ingredient)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return entity is null ? null : _mapper.Map<PurchaseDto>(entity);
    }

    public async Task<PurchaseDto> CreateAsync(CreatePurchaseDto dto, CancellationToken cancellationToken = default)
    {
        var billNumber = dto.BillNumber.Trim();
        if (billNumber.Length == 0)
            throw new InvalidOperationException("Bill number is required.");

        if (dto.Lines.Count == 0)
            throw new InvalidOperationException("At least one line item is required.");

        if (dto.Lines.Select(l => l.IngredientId).Distinct().Count() != dto.Lines.Count)
            throw new InvalidOperationException("Each ingredient can only appear once on a purchase.");

        if (dto.TaxAmount < 0)
            throw new InvalidOperationException("Tax amount cannot be negative.");

        var provider = await _providers.GetByIdAsync(dto.ProviderId, cancellationToken);
        if (provider is null || !provider.IsActive)
            throw new InvalidOperationException("Provider was not found or is inactive.");

        if (await _purchases.Query().AnyAsync(p => p.BillNumber == billNumber, cancellationToken))
            throw new InvalidOperationException("A purchase with this bill number already exists.");

        var ingredientIds = dto.Lines.Select(l => l.IngredientId).ToList();
        var ingredients = await _ingredients.Query()
            .Where(i => ingredientIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (ingredients.Count != ingredientIds.Count)
            throw new InvalidOperationException("One or more ingredients were not found.");

        if (ingredients.Any(i => !i.IsActive))
            throw new InvalidOperationException("All ingredients must be active.");

        var ingredientById = ingredients.ToDictionary(i => i.Id);
        var purchaseId = Guid.NewGuid();
        var lines = new List<PurchaseLine>();
        decimal subtotal = 0;

        foreach (var lineDto in dto.Lines)
        {
            if (lineDto.Quantity <= 0)
                throw new InvalidOperationException("Line quantity must be greater than zero.");

            if (lineDto.UnitPrice < 0)
                throw new InvalidOperationException("Line unit price cannot be negative.");

            var lineTotal = decimal.Round(lineDto.Quantity * lineDto.UnitPrice, 2, MidpointRounding.AwayFromZero);
            subtotal += lineTotal;

            lines.Add(
                new PurchaseLine
                {
                    Id = Guid.NewGuid(),
                    PurchaseId = purchaseId,
                    IngredientId = lineDto.IngredientId,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    LineTotal = lineTotal,
                });
        }

        subtotal = decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        var taxAmount = decimal.Round(dto.TaxAmount, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + taxAmount;

        var purchasedAt = NormalizeUtc(dto.PurchasedAtUtc ?? DateTime.UtcNow);
        var paymentAt = NormalizeUtc(dto.PaymentDateUtc ?? purchasedAt);

        var purchase = new Purchase
        {
            Id = purchaseId,
            ProviderId = dto.ProviderId,
            BillNumber = billNumber,
            PurchasedAtUtc = purchasedAt,
            PaymentDateUtc = paymentAt,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            Total = total,
            Notes = dto.Notes?.Trim(),
        };

        await _purchases.AddAsync(purchase, cancellationToken);
        await _purchaseLines.AddRangeAsync(lines, cancellationToken);

        foreach (var line in lines)
        {
            var ingredient = ingredientById[line.IngredientId];
            var previousQuantity = ingredient.StockQuantity;
            var previousUnitCost = ingredient.UnitCost;
            ingredient.UnitCost = InventoryCosting.ComputeWeightedAverageUnitCost(
                previousQuantity,
                previousUnitCost,
                line.Quantity,
                line.UnitPrice);
            ingredient.StockQuantity = InventoryCosting.AddStock(previousQuantity, line.Quantity);
            _ingredients.Update(ingredient);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(purchaseId, cancellationToken))!;
    }

    public async Task<PurchaseDto?> UpdatePaymentDateAsync(
        Guid id,
        UpdatePurchasePaymentDateDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!dto.PaymentDateUtc.HasValue)
            throw new InvalidOperationException("Payment date is required.");

        var entity = await _purchases.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        entity.PaymentDateUtc = NormalizeUtc(dto.PaymentDateUtc.Value);
        _purchases.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
