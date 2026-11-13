using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Crm.Customers;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

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

    public Task<PagedResult<CustomerDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _customers.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeCustomers(q, query),
            entities => Task.FromResult<IReadOnlyList<CustomerDto>>(_mapper.Map<IReadOnlyList<CustomerDto>>(entities.ToList())),
            cancellationToken);

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _customers.Query().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<CustomerDto>(entity);
    }

    public async Task<CustomerDto?> FindByTaxIdAsync(string taxId, CancellationToken cancellationToken = default)
    {
        var normalized = CustomerTaxId.Normalize(taxId);
        if (normalized is null)
            return null;

        var matches = await _customers.Query()
            .AsNoTracking()
            .Where(c => c.TaxId != null)
            .ToListAsync(cancellationToken);
        var entity = matches.FirstOrDefault(c => CustomerTaxId.Normalize(c.TaxId) == normalized);
        return entity is null ? null : _mapper.Map<CustomerDto>(entity);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default)
    {
        var month = dto.BirthMonth;
        var day = dto.BirthDay;
        CustomerBirthday.Normalize(ref month, ref day);
        var taxId = await EnsureUniqueTaxIdAsync(dto.TaxId, excludeId: null, cancellationToken);

        var entity = new Customer
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Email = NullIfEmpty(dto.Email),
            Phone = NullIfEmpty(dto.Phone),
            TaxId = taxId,
            Address = NullIfEmpty(dto.Address),
            BirthMonth = month,
            BirthDay = day,
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
        entity.Email = NullIfEmpty(dto.Email);
        entity.Phone = NullIfEmpty(dto.Phone);
        entity.TaxId = await EnsureUniqueTaxIdAsync(dto.TaxId, entity.Id, cancellationToken);
        entity.Address = NullIfEmpty(dto.Address);
        var month = dto.BirthMonth;
        var day = dto.BirthDay;
        CustomerBirthday.Normalize(ref month, ref day);
        entity.BirthMonth = month;
        entity.BirthDay = day;
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

    public async Task<CustomerDto> UpsertDeliveryProfileAsync(
        StartDeliveryCustomerDto dto,
        CancellationToken cancellationToken = default)
    {
        var taxId = CustomerTaxId.Normalize(dto.TaxId)
            ?? throw new InvalidOperationException("Indique el número de documento.");
        if (CustomerTaxId.IsConsumidorFinal(taxId))
            throw new InvalidOperationException("No use consumidor final para un domicilio.");

        var name = dto.Name.Trim();
        var phone = dto.Phone.Trim();
        var address = dto.Address.Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Indique el nombre del cliente.");
        if (phone.Length == 0)
            throw new InvalidOperationException("Indique el teléfono.");
        if (address.Length == 0)
            throw new InvalidOperationException("Indique la dirección de entrega.");

        var matches = await _customers.Query()
            .Where(c => c.TaxId != null)
            .ToListAsync(cancellationToken);
        var entity = matches.FirstOrDefault(c => CustomerTaxId.Normalize(c.TaxId) == taxId);

        if (entity is null)
        {
            entity = new Customer
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = NullIfEmpty(dto.Email),
                Phone = phone,
                TaxId = taxId,
                Address = address,
                IsActive = true,
            };
            await _customers.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.Name = name;
            entity.Email = NullIfEmpty(dto.Email);
            entity.Phone = phone;
            entity.TaxId = taxId;
            entity.Address = address;
            entity.IsActive = true;
            _customers.Update(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<CustomerDto>(entity);
    }

    private async Task<string?> EnsureUniqueTaxIdAsync(
        string? raw,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var taxId = CustomerTaxId.Normalize(raw);
        if (taxId is null)
            return null;

        var matches = await _customers.Query()
            .Where(c => c.TaxId != null)
            .ToListAsync(cancellationToken);
        if (matches.Any(c => c.Id != excludeId && CustomerTaxId.Normalize(c.TaxId) == taxId))
            throw new InvalidOperationException("Ya existe un cliente con ese documento.");

        return taxId;
    }

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
