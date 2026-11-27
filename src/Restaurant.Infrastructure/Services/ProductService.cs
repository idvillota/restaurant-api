using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog;
using Restaurant.Application.Features.Catalog.Products;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductService : IProductService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<ProductIngredient> _productIngredients;
    private readonly IRepository<ProductBundleLine> _productBundleLines;
    private readonly IRepository<Ingredient> _ingredients;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ApplicationDbContext _db;
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
        ApplicationDbContext db,
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
        _db = db;
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

    public async Task<ProductMenuDto> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();

        var products = await _products.Query()
            .AsNoTracking()
            .Include(p => p.ProductType)
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return new ProductMenuDto
        {
            Products = products
                .Where(p => p.ProductType is { IsActive: true })
                // Packs are sold from Tiqueteras (customer-first), not from salon orders.
                .Where(p => p.LunchTicketPackSize is null or <= 0)
                .Where(p => !p.ComboOnly)
                .Select(p => MapProduct(p, costPrice: 0m))
                .ToList(),
        };
    }

    public async Task<ProductListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _products.Query().AsNoTracking().Include(p => p.ProductType).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null)
            return null;

        var costPrice = await GetCostPriceAsync(id, cancellationToken);
        return MapProduct(entity, costPrice);
    }

    public async Task<ProductListItemDto?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        var code = NormalizeBarcode(barcode);
        if (code is null)
            return null;

        var entity = await _products.Query()
            .AsNoTracking()
            .Include(p => p.ProductType)
            .FirstOrDefaultAsync(p => p.IsActive && p.Barcode == code, cancellationToken);
        if (entity is null)
            return null;

        var costPrice = await GetCostPriceAsync(entity.Id, cancellationToken);
        return MapProduct(entity, costPrice);
    }

    public async Task<ProductListItemDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        if (!await _productTypes.Query().AnyAsync(t => t.Id == dto.ProductTypeId && t.IsActive, cancellationToken))
            throw new InvalidOperationException("Product type was not found or is inactive.");

        var name = dto.Name.Trim();
        if (await _products.Query().AnyAsync(p => p.IsActive && p.Name == name, cancellationToken))
            throw new InvalidOperationException("An active product with this name already exists.");

        var barcode = NormalizeBarcode(dto.Barcode);
        if (barcode is null && await IsBarcodesEnabledAsync(cancellationToken))
            barcode = await AllocateUniqueBarcodeAsync(name, excludeProductId: null, cancellationToken);
        await EnsureBarcodeAvailableAsync(barcode, excludeProductId: null, cancellationToken);

        var entity = new Product
        {
            Id = Guid.NewGuid(),
            ProductTypeId = dto.ProductTypeId,
            CompositionType = dto.CompositionType,
            Name = name,
            Description = NormalizeDescription(dto.Description),
            Barcode = barcode,
            UnitPrice = dto.UnitPrice,
            IsActive = true,
            ComboOnly = NormalizeComboOnly(dto.CompositionType, dto.ComboOnly),
        };
        await ApplyPostedCostAsync(entity, dto.CostPrice, cancellationToken);
        await _products.AddAsync(entity, cancellationToken);

        if (dto.CompositionType == EProductType.Resale)
            await ApplyResaleRecipeFromDtoAsync(entity.Id, dto.CompositionType, dto.ResaleIngredientId, dto.ResaleQuantity, cancellationToken);

        if (dto.CompositionType == EProductType.Bundle)
            await ApplyBundleFromDtoAsync(entity.Id, dto.BundleLines, cancellationToken);

        if (dto.CompositionType == EProductType.ChoiceCombo)
            await ApplyChoiceGroupsAsync(entity.Id, dto.ChoiceGroups, cancellationToken);

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

        // Keep existing codes stable on edit. Only fill when the product still has none
        // and barcodes are enabled (never silent-regenerate a printed sticker code).
        var barcode = NormalizeBarcode(dto.Barcode);
        if (barcode is null)
        {
            var existing = NormalizeBarcode(entity.Barcode);
            if (existing is not null)
                barcode = existing;
            else if (await IsBarcodesEnabledAsync(cancellationToken))
                barcode = await AllocateUniqueBarcodeAsync(name, excludeProductId: id, cancellationToken);
        }

        await EnsureBarcodeAvailableAsync(barcode, excludeProductId: id, cancellationToken);

        var compositionTypeChanged = entity.CompositionType != dto.CompositionType;
        entity.ProductTypeId = dto.ProductTypeId;
        entity.CompositionType = dto.CompositionType;
        entity.Name = name;
        entity.Description = NormalizeDescription(dto.Description);
        entity.Barcode = barcode;
        entity.UnitPrice = dto.UnitPrice;
        entity.IsActive = dto.IsActive;
        entity.ComboOnly = NormalizeComboOnly(dto.CompositionType, dto.ComboOnly);
        // Lunch-ticket pack/redeemable flags are managed only via Tiqueteras config.
        await ApplyPostedCostAsync(entity, dto.CostPrice, cancellationToken);

        if (compositionTypeChanged)
        {
            if (dto.CompositionType != EProductType.Bundle)
                await ClearBundleLinesAsync(id, cancellationToken);
            if (dto.CompositionType != EProductType.ChoiceCombo)
                await ClearChoiceGroupsAsync(id, cancellationToken);

            if (dto.CompositionType is EProductType.Bundle or EProductType.ChoiceCombo)
                await ClearRecipeLinesAsync(id, cancellationToken);
            else
                await ValidateExistingRecipeMatchesCompositionTypeAsync(id, entity.CompositionType, cancellationToken);
        }

        _products.Update(entity);

        if (dto.CompositionType == EProductType.Resale)
            await ApplyResaleRecipeFromDtoAsync(id, dto.CompositionType, dto.ResaleIngredientId, dto.ResaleQuantity, cancellationToken);

        if (dto.CompositionType == EProductType.Bundle)
            await ApplyBundleFromDtoAsync(id, dto.BundleLines, cancellationToken);

        if (dto.CompositionType == EProductType.ChoiceCombo)
            await ApplyChoiceGroupsAsync(id, dto.ChoiceGroups, cancellationToken);

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
        if (items.Count == 0)
        {
            if (await IsInventoryEnabledAsync(cancellationToken))
                throw new InvalidOperationException("A product must have at least one ingredient.");

            await ClearRecipeLinesAsync(productId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetRecipeAsync(productId, cancellationToken);
        }

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

    public async Task<ChoiceComboDto?> GetChoiceComboAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _products.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null || product.CompositionType != EProductType.ChoiceCombo)
            return null;

        var groups = await _db.ComboGroups
            .AsNoTracking()
            .Where(g => g.ProductId == productId && !g.IsRetired)
            .Include(g => g.Options)
            .ThenInclude(o => o.Product)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return new ChoiceComboDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.UnitPrice,
            Groups = groups.Select(g => new ChoiceComboGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                SortOrder = g.SortOrder,
                Options = g.Options
                    .OrderBy(o => o.SortOrder)
                    .ThenBy(o => o.Product.Name)
                    .Select(o => new ChoiceComboOptionDto
                    {
                        ProductId = o.ProductId,
                        ProductName = o.Product.Name,
                        SortOrder = o.SortOrder,
                    })
                    .ToList(),
            }).ToList(),
        };
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
        {
            if (await IsInventoryEnabledAsync(cancellationToken))
                throw new InvalidOperationException("Resale products require an ingredient.");

            // Inventory off: recipe optional; leave any existing link untouched.
            return;
        }

        var lines = new List<SetProductRecipeLineDto>
        {
            new() { IngredientId = resaleIngredientId.Value, Quantity = resaleQuantity },
        };

        await ReplaceRecipeLinesAsync(productId, compositionType, lines, cancellationToken);
    }

    private async Task<bool> IsInventoryEnabledAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var enabled = await _db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => (bool?)s.InventoryEnabled)
            .FirstOrDefaultAsync(cancellationToken);

        // Missing settings row → treat as inventory on (safe default for existing tenants).
        return enabled ?? true;
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

        if (components.Any(p => p.CompositionType is EProductType.Bundle or EProductType.ChoiceCombo))
            throw new InvalidOperationException("Un combo fijo no puede incluir otro combo.");
    }

    private static bool NormalizeComboOnly(EProductType compositionType, bool requested)
    {
        if (compositionType is EProductType.Bundle or EProductType.ChoiceCombo)
            return false;
        return requested;
    }

    private async Task ApplyChoiceGroupsAsync(
        Guid productId,
        IReadOnlyList<SetChoiceComboGroupDto>? groups,
        CancellationToken cancellationToken)
    {
        if (groups is null || groups.Count == 0)
            throw new InvalidOperationException("Agregue al menos un tema al combo.");

        var normalized = groups
            .Select((group, index) => new SetChoiceComboGroupDto
            {
                Name = group.Name.Trim(),
                SortOrder = index,
                OptionProductIds = group.OptionProductIds.Where(id => id != Guid.Empty).Distinct().ToList(),
            })
            .ToList();

        if (normalized.Any(g => g.Name.Length == 0))
            throw new InvalidOperationException("Cada tema necesita un nombre.");
        if (normalized.Select(g => g.Name.ToUpperInvariant()).Distinct().Count() != normalized.Count)
            throw new InvalidOperationException("Los temas no pueden repetir el nombre.");
        if (normalized.Any(g => g.OptionProductIds.Count == 0))
            throw new InvalidOperationException("Cada tema necesita al menos un producto.");

        var optionIds = normalized.SelectMany(g => g.OptionProductIds).Distinct().ToList();
        if (optionIds.Contains(productId))
            throw new InvalidOperationException("Un combo no puede incluirse a sí mismo.");

        var products = await _products.Query()
            .AsNoTracking()
            .Where(p => optionIds.Contains(p.Id))
            .Select(p => new { p.Id, p.IsActive, p.CompositionType, p.Name })
            .ToListAsync(cancellationToken);
        if (products.Count != optionIds.Count)
            throw new InvalidOperationException("Uno o más productos del combo no existen.");
        if (products.Any(p => !p.IsActive))
            throw new InvalidOperationException("Todos los productos del combo deben estar activos.");
        if (products.Any(p => p.CompositionType is EProductType.Bundle or EProductType.ChoiceCombo))
            throw new InvalidOperationException("Un tema solo puede incluir preparados o reventa, no otro combo.");

        var existing = await _db.ComboGroups
            .Include(g => g.Options)
            .Where(g => g.ProductId == productId)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

        var tenantId = ResolveTenantId();
        var unmatched = existing.ToList();
        foreach (var incoming in normalized)
        {
            var group = TakeMatchingGroup(unmatched, incoming.Name);
            if (group is null)
            {
                group = new ComboGroup
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = productId,
                };
                await _db.ComboGroups.AddAsync(group, cancellationToken);
            }

            group.IsRetired = false;
            group.Name = incoming.Name;
            group.SortOrder = incoming.SortOrder;
            SyncChoiceGroupOptions(group, incoming.OptionProductIds, tenantId);
        }

        if (unmatched.Count > 0)
            await RetireOrRemoveGroupsAsync(unmatched, cancellationToken);
    }

    private static ComboGroup? TakeMatchingGroup(List<ComboGroup> available, string name)
    {
        var index = available.FindIndex(g =>
            !g.IsRetired && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            index = available.FindIndex(g =>
                string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        if (index < 0)
            return null;

        var group = available[index];
        available.RemoveAt(index);
        return group;
    }

    private void SyncChoiceGroupOptions(ComboGroup group, IReadOnlyList<Guid> optionProductIds, Guid tenantId)
    {
        var desired = optionProductIds.ToList();
        var removed = group.Options.Where(option => !desired.Contains(option.ProductId)).ToList();
        foreach (var option in removed)
        {
            group.Options.Remove(option);
            _db.ComboGroupOptions.Remove(option);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var productId = desired[i];
            var option = group.Options.FirstOrDefault(row => row.ProductId == productId);
            if (option is null)
            {
                group.Options.Add(new ComboGroupOption
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ComboGroupId = group.Id,
                    ProductId = productId,
                    SortOrder = i,
                });
                continue;
            }

            option.SortOrder = i;
        }
    }

    private async Task RetireOrRemoveGroupsAsync(
        IReadOnlyList<ComboGroup> groups,
        CancellationToken cancellationToken)
    {
        var groupIds = groups.Select(g => g.Id).ToList();
        var usedIds = await _db.SalesOrderLineChoices
            .AsNoTracking()
            .Where(c => groupIds.Contains(c.ComboGroupId))
            .Select(c => c.ComboGroupId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var used = usedIds.ToHashSet();

        foreach (var group in groups)
        {
            if (used.Contains(group.Id))
            {
                group.IsRetired = true;
                continue;
            }

            _db.ComboGroups.Remove(group);
        }
    }

    private async Task ClearChoiceGroupsAsync(Guid productId, CancellationToken cancellationToken)
    {
        var groups = await _db.ComboGroups
            .Where(g => g.ProductId == productId)
            .ToListAsync(cancellationToken);
        if (groups.Count == 0)
            return;

        await RetireOrRemoveGroupsAsync(groups, cancellationToken);
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
            case EProductType.ChoiceCombo:
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

    private Task<Dictionary<Guid, decimal>> GetCostPricesByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken) =>
        ProductCostCalculator.GetCostPricesByProductIdsAsync(_db, productIds, cancellationToken);

    private async Task ApplyPostedCostAsync(Product entity, decimal? postedCost, CancellationToken cancellationToken)
    {
        if (await IsInventoryEnabledAsync(cancellationToken))
        {
            if (postedCost is > 0)
                throw new InvalidOperationException(
                    "Product cost cannot be set while inventory is enabled; it is calculated from the recipe or combo.");

            return;
        }

        entity.ManualCostPrice = decimal.Round(postedCost ?? 0m, 2, MidpointRounding.AwayFromZero);
    }

    private ProductListItemDto MapProduct(Product product, decimal costPrice)
    {
        var dto = _mapper.Map<ProductListItemDto>(product);
        dto.CostPrice = costPrice;
        dto.ImageUrl = _productImages.GetPublicUrl(product.ImagePath);
        return dto;
    }

    public async Task<int> BackfillMissingBarcodesAsync(CancellationToken cancellationToken = default)
    {
        var missing = await _products.Query()
            .Where(p => p.Barcode == null || p.Barcode == "")
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        if (missing.Count == 0)
            return 0;

        var assigned = 0;
        foreach (var product in missing)
        {
            product.Barcode = await AllocateUniqueBarcodeAsync(product.Name, product.Id, cancellationToken);
            assigned++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return assigned;
    }

    private async Task<bool> IsBarcodesEnabledAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var enabled = await _db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => (bool?)s.BarcodesEnabled)
            .FirstOrDefaultAsync(cancellationToken);
        return enabled ?? true;
    }

    private async Task<string> AllocateUniqueBarcodeAsync(
        string productName,
        Guid? excludeProductId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var candidate = ProductBarcodeCodes.Generate(productName);
            var taken = await _products.Query()
                .AnyAsync(
                    p => p.Barcode == candidate && (excludeProductId == null || p.Id != excludeProductId),
                    cancellationToken);
            if (!taken)
                return candidate;
        }

        throw new InvalidOperationException("No se pudo generar un código de barras único. Intente de nuevo.");
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static string? NormalizeBarcode(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim().ToUpperInvariant();

    private async Task EnsureBarcodeAvailableAsync(
        string? barcode,
        Guid? excludeProductId,
        CancellationToken cancellationToken)
    {
        if (barcode is null)
            return;

        var taken = await _products.Query()
            .AnyAsync(
                p => p.Barcode == barcode && (excludeProductId == null || p.Id != excludeProductId),
                cancellationToken);
        if (taken)
            throw new InvalidOperationException("Otro producto ya usa este código de barras.");
    }
}
