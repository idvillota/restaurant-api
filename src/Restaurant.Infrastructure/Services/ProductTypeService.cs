using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog.ProductTypes;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductTypeService : IProductTypeService
{
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<Product> _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ProductTypeService(
        IRepository<ProductType> productTypes,
        IRepository<Product> products,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _productTypes = productTypes;
        _products = products;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductTypeDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _productTypes.Query().AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Name);
        var list = includeInactive
            ? await query.ToListAsync(cancellationToken)
            : await query.Where(t => t.IsActive).ToListAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<ProductTypeDto>>(list);
    }

    public async Task<ProductTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _productTypes.Query().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<ProductTypeDto>(entity);
    }

    public async Task<ProductTypeDto> CreateAsync(CreateProductTypeDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        if (await _productTypes.Query().AnyAsync(t => t.IsActive && t.Name == name, cancellationToken))
            throw new InvalidOperationException("An active product type with this name already exists.");

        var entity = new ProductType
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = dto.Description?.Trim(),
            SortOrder = dto.SortOrder,
            IsActive = true,
        };
        await _productTypes.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<ProductTypeDto>(entity);
    }

    public async Task<ProductTypeDto?> UpdateAsync(Guid id, UpdateProductTypeDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _productTypes.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        var name = dto.Name.Trim();
        if (await _productTypes.Query().AnyAsync(t => t.Id != id && t.IsActive && t.Name == name, cancellationToken))
            throw new InvalidOperationException("Another active product type already uses this name.");

        entity.Name = name;
        entity.Description = dto.Description?.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        _productTypes.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<ProductTypeDto>(entity);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _productTypes.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        if (await _products.Query().AnyAsync(p => p.ProductTypeId == id && p.IsActive, cancellationToken))
            throw new InvalidOperationException("Cannot deactivate a product type that still has active products.");

        entity.IsActive = false;
        _productTypes.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
