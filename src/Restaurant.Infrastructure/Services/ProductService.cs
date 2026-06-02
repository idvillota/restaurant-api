using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog;
using Restaurant.Application.Features.Catalog.Products;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductService : IProductService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<ProductIngredient> _productIngredients;
    private readonly IRepository<ProductBundleLine> _productBundleLines;
    private readonly IRepository<Ingredient> _ingredients;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IProductImageStorage _productImages;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ProductService(
        IRepository<Product> products,
        IRepository<ProductType> productTypes,
        IRepository<ProductIngredient> productIngredients,
        IRepository<ProductBundleLine> productBundleLines,
        IRepository<Ingredient> ingredients,
        ICurrentTenantContext tenantContext,
        IProductImageStorage productImages,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _products = products;
        _productTypes = productTypes;
        _productIngredients = productIngredients;
        _productBundleLines = productBundleLines;
        _ingredients = ingredients;
        _tenantContext = tenantContext;
        _productImages = productImages;
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
            CompositionType = dto.CompositionType,
            Name = name,
            Description = NormalizeDescription(dto.Description),
            Sku = sku,
            UnitPrice = dto.UnitPrice,
            IsActive = true,
        };
        await _products.AddAsync(entity, cancellationToken);

        if (dto.CompositionType == EProductType.Resale)
            await ApplyResaleRecipeFromDtoAsync(entity.Id, dto.CompositionType, dto.ResaleIngredientId, dto.ResaleQuantity, cancellationToken);

        if (dto.CompositionType == EProductType.Bundle)
            await ApplyBundleFromDtoAsync(entity.Id, dto.BundleLines, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _products.Query().AsNoTracking().Include(p => p.ProductType).FirstAsync(p => p.Id == entity.Id, cancellationToken);
        var costPrice = await GetCostPriceAsync(entity.Id, cancellationToken);
        return MapProduct(entity, costPrice);
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

        var compositionTypeChanged = entity.CompositionType != dto.CompositionType;
        entity.ProductTypeId = dto.ProductTypeId;
        entity.CompositionType = dto.CompositionType;
        entity.Name = name;
        entity.Description = NormalizeDescription(dto.Description);
        entity.Sku = sku;
        entity.UnitPrice = dto.UnitPrice;
        entity.IsActive = dto.IsActive;

        if (compositionTypeChanged)
        {
            if (dto.CompositionType == EProductType.Bundle)
                await ClearRecipeLinesAsync(id, cancellationToken);
            else
                await ClearBundleLinesAsync(id, cancellationToken);

            if (dto.CompositionType != EProductType.Bundle)
                await ValidateExistingRecipeMatchesCompositionTypeAsync(id, entity.CompositionType, cancellationToken);
        }

        _products.Update(entity);

        if (dto.CompositionType == EProductType.Resale)
            await ApplyResaleRecipeFromDtoAsync(id, dto.CompositionType, dto.ResaleIngredientId, dto.ResaleQuantity, cancellationToken);

        if (dto.CompositionType == EProductType.Bundle)
            await ApplyBundleFromDtoAsync(id, dto.BundleLines, cancellationToken);

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

    public async Task<ProductListItemDto?> SetImageAsync(
        Guid productId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _products.GetByIdAsync(productId, cancellationToken);
        if (entity is null)
            return null;

        var tenantId = ResolveTenantId();
        var previousPath = entity.ImagePath;

        var relativePath = await _productImages.SaveAsync(tenantId, productId, content, fileName, cancellationToken);
        entity.ImagePath = relativePath;
        _products.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousPath, relativePath, StringComparison.OrdinalIgnoreCase))
            await _productImages.DeleteIfExistsAsync(previousPath, cancellationToken);

        return await GetByIdAsync(productId, cancellationToken);
    }

    public async Task<ProductListItemDto?> RemoveImageAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var entity = await _products.GetByIdAsync(productId, cancellationToken);
        if (entity is null)
            return null;

        var previousPath = entity.ImagePath;
        if (string.IsNullOrWhiteSpace(previousPath))
            return await GetByIdAsync(productId, cancellationToken);

        entity.ImagePath = null;
        _products.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _productImages.DeleteIfExistsAsync(previousPath, cancellationToken);

        return await GetByIdAsync(productId, cancellationToken);
    }

    public async Task<ProductRecipeDto?> SetRecipeAsync(Guid productId, SetProductRecipeDto dto, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken);
        if (product is null)
            return null;

        if (product.CompositionType == EProductType.Bundle)
            throw new InvalidOperationException("Los combos usan productos incluidos, no una receta de ingredientes.");

        var items = dto.Lines ?? [];
        await ReplaceRecipeLinesAsync(productId, product.CompositionType, items, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetRecipeAsync(productId, cancellationToken);
    }

    public async Task<ProductBundleDto?> GetBundleAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _products.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return null;

        var lines = await _productBundleLines.Query()
            .AsNoTracking()
            .Where(bl => bl.ProductId == productId)
            .Include(bl => bl.ComponentProduct)
            .OrderBy(bl => bl.SortOrder)
            .ThenBy(bl => bl.ComponentProduct.Name)
            .ToListAsync(cancellationToken);

        var componentIds = lines.Select(l => l.ComponentProductId).Distinct().ToList();
        var componentCosts = await GetCostPricesByProductIdsAsync(componentIds, cancellationToken);

        return BuildBundleDto(product, lines, componentCosts);
    }

    public async Task<ProductBundleDto?> SetBundleAsync(Guid productId, SetProductBundleDto dto, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken);
        if (product is null)
            return null;

        if (product.CompositionType != EProductType.Bundle)
            throw new InvalidOperationException("Solo los productos combo pueden tener componentes.");

        var items = dto.Lines ?? [];
        await ReplaceBundleLinesAsync(productId, items, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetBundleAsync(productId, cancellationToken);
    }

    private async Task ApplyResaleRecipeFromDtoAsync(
        Guid productId,
        EProductType compositionType,
        Guid? resaleIngredientId,
        decimal resaleQuantity,
        CancellationToken cancellationToken)
    {
        if (compositionType != EProductType.Resale)
            return;

        if (resaleIngredientId is null || resaleIngredientId == Guid.Empty)
            throw new InvalidOperationException("Resale products require an ingredient.");

        var lines = new List<SetProductRecipeLineDto>
        {
            new() { IngredientId = resaleIngredientId.Value, Quantity = resaleQuantity },
        };

        await ReplaceRecipeLinesAsync(productId, compositionType, lines, cancellationToken);
    }

    private async Task ApplyBundleFromDtoAsync(
        Guid productId,
        IReadOnlyList<SetProductBundleLineDto>? bundleLines,
        CancellationToken cancellationToken)
    {
        if (bundleLines is null || bundleLines.Count == 0)
            throw new InvalidOperationException("Los combos requieren al menos un producto incluido.");

        await ReplaceBundleLinesAsync(productId, bundleLines, cancellationToken);
    }

    private async Task ReplaceBundleLinesAsync(
        Guid productId,
        IReadOnlyList<SetProductBundleLineDto> items,
        CancellationToken cancellationToken)
    {
        await ValidateBundleLinesAsync(productId, items, cancellationToken);

        var existing = await _productBundleLines.Query()
            .Where(bl => bl.ProductId == productId)
            .ToListAsync(cancellationToken);

        var incoming = items
            .Select((line, index) => new
            {
                line.ComponentProductId,
                line.Quantity,
                SortOrder = line.SortOrder > 0 ? line.SortOrder : index,
            })
            .ToDictionary(x => x.ComponentProductId);

        foreach (var row in existing)
        {
            if (!incoming.TryGetValue(row.ComponentProductId, out var line))
            {
                _productBundleLines.Remove(row);
                continue;
            }

            row.Quantity = line.Quantity;
            row.SortOrder = line.SortOrder;
            _productBundleLines.Update(row);
            incoming.Remove(row.ComponentProductId);
        }

        foreach (var (componentProductId, line) in incoming)
        {
            await _productBundleLines.AddAsync(
                new ProductBundleLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = ResolveTenantId(),
                    ProductId = productId,
                    ComponentProductId = componentProductId,
                    Quantity = line.Quantity,
                    SortOrder = line.SortOrder,
                },
                cancellationToken);
        }
    }

    private async Task ValidateBundleLinesAsync(
        Guid productId,
        IReadOnlyList<SetProductBundleLineDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Los combos requieren al menos un producto incluido.");

        if (items.Select(i => i.ComponentProductId).Distinct().Count() != items.Count)
            throw new InvalidOperationException("No se permiten productos duplicados en el combo.");

        if (items.Any(i => i.ComponentProductId == productId))
            throw new InvalidOperationException("Un combo no puede incluirse a sí mismo.");

        if (items.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("Cada cantidad de componente debe ser mayor que cero.");

        var componentIds = items.Select(i => i.ComponentProductId).ToList();
        var components = await _products.Query()
            .AsNoTracking()
            .Where(p => componentIds.Contains(p.Id))
            .Select(p => new { p.Id, p.IsActive, p.CompositionType, p.Name })
            .ToListAsync(cancellationToken);

        if (components.Count != componentIds.Count)
            throw new InvalidOperationException("Uno o más productos incluidos no existen.");

        if (components.Any(p => !p.IsActive))
            throw new InvalidOperationException("Todos los productos incluidos deben estar activos.");

        if (components.Any(p => p.CompositionType == EProductType.Bundle))
            throw new InvalidOperationException("Un combo no puede incluir otro combo.");
    }

    private async Task ClearRecipeLinesAsync(Guid productId, CancellationToken cancellationToken)
    {
        var existing = await _productIngredients.Query()
            .Where(pi => pi.ProductId == productId)
            .ToListAsync(cancellationToken);

        foreach (var row in existing)
            _productIngredients.Remove(row);
    }

    private async Task ClearBundleLinesAsync(Guid productId, CancellationToken cancellationToken)
    {
        var existing = await _productBundleLines.Query()
            .Where(bl => bl.ProductId == productId)
            .ToListAsync(cancellationToken);

        foreach (var row in existing)
            _productBundleLines.Remove(row);
    }

    private async Task ReplaceRecipeLinesAsync(
        Guid productId,
        EProductType compositionType,
        IReadOnlyList<SetProductRecipeLineDto> items,
        CancellationToken cancellationToken)
    {
        ValidateRecipeLines(compositionType, items);

        var ingredientIds = items.Select(i => i.IngredientId).ToList();
        var activeCount = await _ingredients.Query()
            .CountAsync(i => ingredientIds.Contains(i.Id) && i.IsActive, cancellationToken);
        if (activeCount != ingredientIds.Count)
            throw new InvalidOperationException("One or more ingredients were not found or are inactive.");

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
                    TenantId = ResolveTenantId(),
                    ProductId = productId,
                    IngredientId = ingredientId,
                    Quantity = quantity,
                },
                cancellationToken);
        }
    }

    private Guid ResolveTenantId()
    {
        if (_tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;

        throw new InvalidOperationException("Tenant context is not available.");
    }

    private static ProductRecipeDto BuildRecipeDto(Product product, List<ProductIngredient> lines)
    {
        var lineDtos = lines.Select(MapLine).ToList();
        return new ProductRecipeDto
        {
            ProductId = product.Id,
            CompositionType = product.CompositionType,
            ProductName = product.Name,
            UnitPrice = product.UnitPrice,
            CostPrice = lineDtos.Sum(l => l.LineCost),
            Lines = lineDtos,
        };
    }

    internal static void ValidateRecipeLines(EProductType compositionType, IReadOnlyList<SetProductRecipeLineDto> items)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("A product must have at least one ingredient.");

        if (items.Select(i => i.IngredientId).Distinct().Count() != items.Count)
            throw new InvalidOperationException("Duplicate ingredients are not allowed.");

        if (items.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("Each ingredient quantity must be greater than zero.");

        switch (compositionType)
        {
            case EProductType.Resale:
                if (items.Count != 1)
                    throw new InvalidOperationException("Resale products must have exactly one ingredient.");
                if (items[0].Quantity != 1m)
                    throw new InvalidOperationException("Resale products must use a quantity of 1.");
                break;
            case EProductType.Prepared:
                break;
            case EProductType.Bundle:
                throw new InvalidOperationException("Los combos usan productos incluidos, no ingredientes.");
            default:
                throw new InvalidOperationException("Unknown product composition type.");
        }
    }

    private async Task ValidateExistingRecipeMatchesCompositionTypeAsync(
        Guid productId,
        EProductType compositionType,
        CancellationToken cancellationToken)
    {
        var lines = await _productIngredients.Query()
            .AsNoTracking()
            .Where(pi => pi.ProductId == productId)
            .Select(pi => new SetProductRecipeLineDto { IngredientId = pi.IngredientId, Quantity = pi.Quantity })
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
            return;

        ValidateRecipeLines(compositionType, lines);
    }

    private static ProductBundleDto BuildBundleDto(
        Product product,
        List<ProductBundleLine> lines,
        IReadOnlyDictionary<Guid, decimal> componentCosts)
    {
        var lineDtos = lines.Select(line =>
        {
            var componentCost = componentCosts.GetValueOrDefault(line.ComponentProductId);
            return new ProductBundleLineDto
            {
                Id = line.Id,
                ComponentProductId = line.ComponentProductId,
                ComponentProductName = line.ComponentProduct.Name,
                ComponentCompositionType = line.ComponentProduct.CompositionType,
                ComponentUnitPrice = line.ComponentProduct.UnitPrice,
                ComponentCostPrice = componentCost,
                Quantity = line.Quantity,
                LineCost = componentCost * line.Quantity,
                SortOrder = line.SortOrder,
            };
        }).ToList();

        return new ProductBundleDto
        {
            ProductId = product.Id,
            CompositionType = product.CompositionType,
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

        var compositionByProduct = await _products.Query()
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CompositionType })
            .ToDictionaryAsync(p => p.Id, p => p.CompositionType, cancellationToken);

        var recipeCosts = await (
            from pi in _productIngredients.Query().AsNoTracking()
            join ing in _ingredients.Query().AsNoTracking() on pi.IngredientId equals ing.Id
            where productIds.Contains(pi.ProductId)
            select new { pi.ProductId, pi.Quantity, ing.UnitCost }
        ).ToListAsync(cancellationToken);

        var recipeCostByProduct = recipeCosts
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(r => ComputeLineCost(r.Quantity, r.UnitCost)));

        var bundleLines = await _productBundleLines.Query()
            .AsNoTracking()
            .Where(bl => productIds.Contains(bl.ProductId))
            .Select(bl => new { bl.ProductId, bl.ComponentProductId, bl.Quantity })
            .ToListAsync(cancellationToken);

        var componentIds = bundleLines.Select(bl => bl.ComponentProductId).Distinct().ToList();
        var componentCosts = componentIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await GetCostPricesByProductIdsAsync(componentIds, cancellationToken);

        var bundleCostByProduct = bundleLines
            .GroupBy(bl => bl.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(bl => componentCosts.GetValueOrDefault(bl.ComponentProductId) * bl.Quantity));

        var result = new Dictionary<Guid, decimal>();
        foreach (var productId in productIds)
        {
            if (!compositionByProduct.TryGetValue(productId, out var compositionType))
                continue;

            result[productId] = compositionType switch
            {
                EProductType.Bundle => bundleCostByProduct.GetValueOrDefault(productId),
                _ => recipeCostByProduct.GetValueOrDefault(productId),
            };
        }

        return result;
    }

    private ProductListItemDto MapProduct(Product product, decimal costPrice)
    {
        var dto = _mapper.Map<ProductListItemDto>(product);
        dto.CostPrice = costPrice;
        dto.ImageUrl = _productImages.GetPublicUrl(product.ImagePath);
        return dto;
    }

    private static string? NormalizeSku(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
