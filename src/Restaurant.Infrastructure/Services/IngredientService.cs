using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog.Ingredients;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class IngredientService : IIngredientService
{
    private readonly IRepository<Ingredient> _ingredients;
    private readonly IRepository<IngredientCategory> _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public IngredientService(
        IRepository<Ingredient> ingredients,
        IRepository<IngredientCategory> categories,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _ingredients = ingredients;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<IngredientDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _ingredients.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeIngredients(q, query),
            entities => Task.FromResult<IReadOnlyList<IngredientDto>>(_mapper.Map<IReadOnlyList<IngredientDto>>(entities.ToList())),
            cancellationToken);

    public async Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _ingredients.Query()
            .AsNoTracking()
            .Include(i => i.IngredientCategory)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<IngredientDto>(entity);
    }

    public async Task<IngredientDto> CreateAsync(CreateIngredientDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(dto.IngredientCategoryId, cancellationToken);

        var name = dto.Name.Trim();
        var exists = await _ingredients.Query().AnyAsync(
            i => i.IsActive && i.Name == name,
            cancellationToken);
        if (exists)
            throw new InvalidOperationException("An active ingredient with this name already exists.");

        var entity = new Ingredient
        {
            Id = Guid.NewGuid(),
            IngredientCategoryId = dto.IngredientCategoryId,
            Name = name,
            Unit = dto.Unit!.Value,
            UnitCost = dto.UnitCost is > 0 ? dto.UnitCost : null,
            StockQuantity = null,
            ReorderLevel = dto.ReorderLevel,
            IsActive = true,
        };

        await _ingredients.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _ingredients.Query()
            .AsNoTracking()
            .Include(i => i.IngredientCategory)
            .FirstAsync(i => i.Id == entity.Id, cancellationToken);

        return _mapper.Map<IngredientDto>(entity);
    }

    public async Task<IngredientDto?> UpdateAsync(Guid id, UpdateIngredientDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _ingredients.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        await EnsureCategoryExistsAsync(dto.IngredientCategoryId, cancellationToken);

        var name = dto.Name.Trim();
        var duplicate = await _ingredients.Query().AnyAsync(
            i => i.Id != id && i.IsActive && i.Name == name,
            cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("Another active ingredient already uses this name.");

        entity.IngredientCategoryId = dto.IngredientCategoryId;
        entity.Name = name;
        entity.Unit = dto.Unit!.Value;
        entity.ReorderLevel = dto.ReorderLevel;
        entity.IsActive = dto.IsActive;

        _ingredients.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _ingredients.Query()
            .AsNoTracking()
            .Include(i => i.IngredientCategory)
            .FirstAsync(i => i.Id == id, cancellationToken);

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

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var ok = await _categories.Query().AnyAsync(c => c.Id == categoryId && c.IsActive, cancellationToken);
        if (!ok)
            throw new InvalidOperationException("Ingredient category was not found or is inactive.");
    }
}
