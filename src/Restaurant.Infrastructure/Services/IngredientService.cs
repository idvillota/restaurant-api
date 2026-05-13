using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog.Ingredients;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Services;

public sealed class IngredientService : IIngredientService
{
    private readonly IRepository<Ingredient> _ingredients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public IngredientService(IRepository<Ingredient> ingredients, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _ingredients = ingredients;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<IngredientDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _ingredients.Query().AsNoTracking().OrderBy(i => i.Name);
        var list = includeInactive
            ? await query.ToListAsync(cancellationToken)
            : await query.Where(i => i.IsActive).ToListAsync(cancellationToken);

        return _mapper.Map<IReadOnlyList<IngredientDto>>(list);
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _ingredients.Query().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<IngredientDto>(entity);
    }

    public async Task<IngredientDto> CreateAsync(CreateIngredientDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var exists = await _ingredients.Query().AnyAsync(
            i => i.IsActive && i.Name == name,
            cancellationToken);
        if (exists)
            throw new InvalidOperationException("An active ingredient with this name already exists.");

        var entity = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = name,
            Unit = dto.Unit!.Value,
            StockQuantity = dto.StockQuantity,
            ReorderLevel = dto.ReorderLevel,
            IsActive = true,
        };

        await _ingredients.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<IngredientDto>(entity);
    }

    public async Task<IngredientDto?> UpdateAsync(Guid id, UpdateIngredientDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _ingredients.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        var name = dto.Name.Trim();
        var duplicate = await _ingredients.Query().AnyAsync(
            i => i.Id != id && i.IsActive && i.Name == name,
            cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("Another active ingredient already uses this name.");

        entity.Name = name;
        entity.Unit = dto.Unit!.Value;
        entity.StockQuantity = dto.StockQuantity;
        entity.ReorderLevel = dto.ReorderLevel;
        entity.IsActive = dto.IsActive;

        _ingredients.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<IngredientDto>(entity);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _ingredients.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        entity.IsActive = false;
        _ingredients.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
