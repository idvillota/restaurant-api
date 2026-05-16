using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog;
using Restaurant.Application.Features.Catalog.Products;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductService : IProductService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ProductService(
        IRepository<Product> products,
        IRepository<ProductType> productTypes,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _products = products;
        _productTypes = productTypes;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _products.Query().AsNoTracking().Include(p => p.ProductType).OrderBy(p => p.Name);
        var list = includeInactive
            ? await query.ToListAsync(cancellationToken)
            : await query.Where(p => p.IsActive).ToListAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<ProductListItemDto>>(list);
    }

    public async Task<ProductListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _products.Query().AsNoTracking().Include(p => p.ProductType).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<ProductListItemDto>(entity);
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
        return _mapper.Map<ProductListItemDto>(entity);
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
        return _mapper.Map<ProductListItemDto>(entity);
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

    private static string? NormalizeSku(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
