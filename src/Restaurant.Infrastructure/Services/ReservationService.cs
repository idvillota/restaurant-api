using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Reservations;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class ReservationService : IReservationService
{
    private readonly IRepository<Reservation> _reservations;
    private readonly IRepository<Customer> _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ReservationService(
        IRepository<Reservation> reservations,
        IRepository<Customer> customers,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _reservations = reservations;
        _customers = customers;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<ReservationDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _reservations.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeReservations(q, query),
            entities => Task.FromResult<IReadOnlyList<ReservationDto>>(_mapper.Map<IReadOnlyList<ReservationDto>>(entities.ToList())),
            cancellationToken);

    public async Task<ReservationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _reservations.Query().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<ReservationDto>(entity);
    }

    public async Task<ReservationDto> CreateAsync(CreateReservationDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateCustomerAsync(dto.CustomerId, cancellationToken);
        ValidateWindow(dto.StartAtUtc!.Value, dto.EndAtUtc!.Value);

        var entity = new Reservation
        {
            Id = Guid.NewGuid(),
            CustomerId = dto.CustomerId,
            ContactName = dto.ContactName.Trim(),
            ContactEmail = dto.ContactEmail?.Trim(),
            ContactPhone = dto.ContactPhone?.Trim(),
            PartySize = dto.PartySize,
            StartAtUtc = dto.StartAtUtc.Value,
            EndAtUtc = dto.EndAtUtc.Value,
            Status = dto.Status,
            Notes = dto.Notes?.Trim(),
        };
        await _reservations.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<ReservationDto>(entity);
    }

    public async Task<ReservationDto?> UpdateAsync(Guid id, UpdateReservationDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _reservations.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        await ValidateCustomerAsync(dto.CustomerId, cancellationToken);
        ValidateWindow(dto.StartAtUtc!.Value, dto.EndAtUtc!.Value);

        entity.CustomerId = dto.CustomerId;
        entity.ContactName = dto.ContactName.Trim();
        entity.ContactEmail = dto.ContactEmail?.Trim();
        entity.ContactPhone = dto.ContactPhone?.Trim();
        entity.PartySize = dto.PartySize;
        entity.StartAtUtc = dto.StartAtUtc.Value;
        entity.EndAtUtc = dto.EndAtUtc.Value;
        entity.Status = dto.Status;
        entity.Notes = dto.Notes?.Trim();
        _reservations.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<ReservationDto>(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _reservations.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return false;

        _reservations.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ValidateCustomerAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        if (!customerId.HasValue)
            return;

        if (!await _customers.Query().AnyAsync(c => c.Id == customerId && c.IsActive, cancellationToken))
            throw new InvalidOperationException("Customer was not found or is inactive.");
    }

    private static void ValidateWindow(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
            throw new InvalidOperationException("End time must be after start time.");
    }
}
