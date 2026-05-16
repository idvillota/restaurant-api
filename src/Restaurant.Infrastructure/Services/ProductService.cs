using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog;
using Restaurant.Application.Features.Catalog.Products;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductService : IProductService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<ProductIngredient> _productIngredients;
    private readonly IRepository<Ingredient> _ingredients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ProductService(
        IRepository<Product> products,
        IRepository<ProductType> productTypes,
        IRepository<ProductIngredient> productIngredients,
        IRepository<Ingredient> ingredients,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _products = products;
        _productTypes = productTypes;
        _productIngredients = productIngredients;
        _ingredients = ingredients;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<ProductListItemDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync<Product, ProductListItemDto>(
            _products.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeProducts(q, query),
            async entities =>
            {
                var products = entities.ToList();
                var costs = await GetCostPricesByProductIdsAsync(products.Select(p => p.Id).ToList(), cancellationToken);
                IReadOnlyList<ProductListItemDto> items = products
                    .Select(p => MapProduct(p, costs.GetValueOrDefault(p.Id)))
                    .ToList();
                return items;
            },
            cancellationToken);

    public async Task<ProductListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _products.Query().AsNoTracking().Include(p => p.ProductType).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null)
            return null;

        var costPrice = await GetCostPriceAsync(id, cancellationToken);
        return MapProduct(entity, costPrice);
    }

    public async Task<ProductListItemDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        if (!await _productTypes.Query().AnyAsync(t => t.Id == dto.ProductTypeId && t.IsActive, cancellationToken))
            throw new InvalidOperationException("Product type was not found or is inactive.");

        var name = dto.Name.Trim();
        if (await _products.Query().AnyAsync(p => p.IsActive && p.Name == name, cancellationToken))
            throw new InvalidOperationException("An active product with this name already exists.");

        var sku = NormalizeSku(dto.Sku);
        if (sku is not null && await _products.Query().AnyAsync(p => p.IsActive && p.Sku == sku, cancellationToken))
            throw new InvalidOperationException("An active product with this SKU already exists.");

        var entity = new Product
        {
            Id = Guid.NewGuid(),
            ProductTypeId = dto.ProductTypeId,
            Name = name,
            Description = NormalizeDescription(dto.Description),
            Sku = sku,
            UnitPrice = dto.UnitPrice,
            IsActive = true,
        };
        await _products.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _products.Query().AsNoTracking().Include(p => p.ProductType).FirstAsync(p => p.Id == entity.Id, cancellationToken);
        return MapProduct(entity, 0m);
    }

    public async Task<ProductListItemDto?> UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _products.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        if (!await _productTypes.Query().AnyAsync(t => t.Id == dto.ProductTypeId && t.IsActive, cancellationToken))
            throw new InvalidOperationException("Product type was not found or is inactive.");

        var name = dto.Name.Trim();
        if (await _products.Query().AnyAsync(p => p.Id != id && p.IsActive && p.Name == name, cancellationToken))
            throw new InvalidOperationException("Another active product already uses this name.");

        var sku = NormalizeSku(dto.Sku);
        if (sku is not null && await _products.Query().AnyAsync(p => p.Id != id && p.IsActive && p.Sku == sku, cancellationToken))
            throw new InvalidOperationException("Another active product already uses this SKU.");

        entity.ProductTypeId = dto.ProductTypeId;
        entity.Name = name;
        entity.Description = NormalizeDescription(dto.Description);
        entity.Sku = sku;
        entity.UnitPrice = dto.UnitPrice;
        entity.IsActive = dto.IsActive;
        _products.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _products.Query().AsNoTracking().Include(p => p.ProductType).FirstAsync(p => p.Id == id, cancellationToken);
        var costPrice = await GetCostPriceAsync(id, cancellationToken);
        return MapProduct(entity, costPrice);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _products.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        entity.IsActive = false;
        _products.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ProductRecipeDto?> GetRecipeAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _products.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return null;

        var lines = await _productIngredients.Query()
            .AsNoTracking()
            .Where(pi => pi.ProductId == productId)
            .Include(pi => pi.Ingredient)
            .ThenInclude(i => i.IngredientCategory)
            .OrderBy(pi => pi.Ingredient.Name)
            .ToListAsync(cancellationToken);

        return BuildRecipeDto(product, lines);
    }

    public async Task<ProductRecipeDto?> SetRecipeAsync(Guid productId, SetProductRecipeDto dto, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken);
        if (product is null)
            return null;

        var items = dto.Lines ?? [];
        var ingredientIds = items.Select(i => i.IngredientId).ToList();
        if (ingredientIds.Distinct().Count() != ingredientIds.Count)
            throw new InvalidOperationException("Duplicate ingredients are not allowed.");

        if (items.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("Each ingredient quantity must be greater than zero.");

        if (ingredientIds.Count > 0)
        {
            var activeCount = await _ingredients.Query()
                .CountAsync(i => ingredientIds.Contains(i.Id) && i.IsActive, cancellationToken);
            if (activeCount != ingredientIds.Count)
                throw new InvalidOperationException("One or more ingredients were not found or are inactive.");
        }

        var existing = await _productIngredients.Query()
            .Where(pi => pi.ProductId == productId)
            .ToListAsync(cancellationToken);

        var incoming = items.ToDictionary(i => i.IngredientId, i => i.Quantity);

        foreach (var row in existing)
        {
            if (!incoming.TryGetValue(row.IngredientId, out var quantity))
            {
                _productIngredients.Remove(row);
                continue;
            }

            row.Quantity = quantity;
            _productIngredients.Update(row);
            incoming.Remove(row.IngredientId);
        }

        foreach (var (ingredientId, quantity) in incoming)
        {
            await _productIngredients.AddAsync(
                new ProductIngredient
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    IngredientId = ingredientId,
                    Quantity = quantity,
                },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetRecipeAsync(productId, cancellationToken);
    }

    private static ProductRecipeDto BuildRecipeDto(Product product, List<ProductIngredient> lines)
    {
        var lineDtos = lines.Select(MapLine).ToList();
        return new ProductRecipeDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.UnitPrice,
            CostPrice = lineDtos.Sum(l => l.LineCost),
            Lines = lineDtos,
        };
    }

    private static ProductRecipeLineDto MapLine(ProductIngredient pi)
    {
        var ingredient = pi.Ingredient;
        var lineCost = ComputeLineCost(pi.Quantity, ingredient.UnitCost);
        return new ProductRecipeLineDto
        {
            Id = pi.Id,
            IngredientId = pi.IngredientId,
            IngredientName = ingredient.Name,
            IngredientCategoryId = ingredient.IngredientCategoryId,
            IngredientCategoryName = ingredient.IngredientCategory.Name,
            Unit = ingredient.Unit,
            UnitCost = ingredient.UnitCost,
            Quantity = pi.Quantity,
            LineCost = lineCost,
        };
    }

    internal static decimal ComputeLineCost(decimal quantity, decimal? unitCost) =>
        quantity * (unitCost ?? 0m);

    private async Task<decimal> GetCostPriceAsync(Guid productId, CancellationToken cancellationToken)
    {
        var costs = await GetCostPricesByProductIdsAsync([productId], cancellationToken);
        return costs.GetValueOrDefault(productId);
    }

    private async Task<Dictionary<Guid, decimal>> GetCostPricesByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        return await _productIngredients.Query()
            .AsNoTracking()
            .Where(pi => productIds.Contains(pi.ProductId))
            .GroupBy(pi => pi.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                CostPrice = g.Sum(pi => pi.Quantity * (pi.Ingredient.UnitCost ?? 0m)),
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.CostPrice, cancellationToken);
    }

    private ProductListItemDto MapProduct(Product product, decimal costPrice)
    {
        var dto = _mapper.Map<ProductListItemDto>(product);
        dto.CostPrice = costPrice;
        return dto;
    }

    private static string? NormalizeSku(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
