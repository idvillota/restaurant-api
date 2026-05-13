using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Crm.Customers;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Reservation> _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CustomerService(
        IRepository<Customer> customers,
        IRepository<Reservation> reservations,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _customers = customers;
        _reservations = reservations;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _customers.Query().AsNoTracking().OrderBy(c => c.Name);
        var list = includeInactive
            ? await query.ToListAsync(cancellationToken)
            : await query.Where(c => c.IsActive).ToListAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<CustomerDto>>(list);
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _customers.Query().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<CustomerDto>(entity);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new Customer
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            TaxId = dto.TaxId?.Trim(),
            IsActive = true,
        };
        await _customers.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<CustomerDto>(entity);
    }

    public async Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _customers.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        entity.Name = dto.Name.Trim();
        entity.Email = dto.Email?.Trim();
        entity.Phone = dto.Phone?.Trim();
        entity.TaxId = dto.TaxId?.Trim();
        entity.IsActive = dto.IsActive;
        _customers.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<CustomerDto>(entity);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _customers.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        if (await _reservations.Query().AnyAsync(r => r.CustomerId == id, cancellationToken))
            throw new InvalidOperationException("Cannot deactivate a customer that is linked to reservations.");

        entity.IsActive = false;
        _customers.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
