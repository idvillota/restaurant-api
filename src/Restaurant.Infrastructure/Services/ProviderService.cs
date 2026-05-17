using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Procurement.Providers;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class ProviderService : IProviderService
{
    private readonly IRepository<Provider> _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ProviderService(IRepository<Provider> providers, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<ProviderDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _providers.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeProviders(q, query),
            entities => Task.FromResult<IReadOnlyList<ProviderDto>>(_mapper.Map<IReadOnlyList<ProviderDto>>(entities.ToList())),
            cancellationToken);

    public async Task<ProviderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _providers.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<ProviderDto>(entity);
    }

    public async Task<ProviderDto> CreateAsync(CreateProviderDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        if (await _providers.Query().AnyAsync(p => p.IsActive && p.Name == name, cancellationToken))
            throw new InvalidOperationException("An active provider with this name already exists.");

        var entity = new Provider
        {
            Id = Guid.NewGuid(),
            Name = name,
            ContactName = dto.ContactName?.Trim(),
            Address = dto.Address?.Trim(),
            TaxId = dto.TaxId?.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            Notes = dto.Notes?.Trim(),
            IsActive = true,
        };
        await _providers.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<ProviderDto>(entity);
    }

    public async Task<ProviderDto?> UpdateAsync(Guid id, UpdateProviderDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _providers.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        var name = dto.Name.Trim();
        if (await _providers.Query().AnyAsync(p => p.Id != id && p.IsActive && p.Name == name, cancellationToken))
            throw new InvalidOperationException("Another active provider already uses this name.");

        entity.Name = name;
        entity.ContactName = dto.ContactName?.Trim();
        entity.Address = dto.Address?.Trim();
        entity.TaxId = dto.TaxId?.Trim();
        entity.Email = dto.Email?.Trim();
        entity.Phone = dto.Phone?.Trim();
        entity.Notes = dto.Notes?.Trim();
        entity.IsActive = dto.IsActive;
        _providers.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<ProviderDto>(entity);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _providers.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        entity.IsActive = false;
        _providers.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
