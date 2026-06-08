using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Reservations;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class ReservationService : IReservationService
{
    private readonly IRepository<Reservation> _reservations;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<DiningTable> _tables;
    private readonly IRepository<ReservationTable> _reservationTables;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ReservationService(
        IRepository<Reservation> reservations,
        IRepository<Customer> customers,
        IRepository<DiningTable> tables,
        IRepository<ReservationTable> reservationTables,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _reservations = reservations;
        _customers = customers;
        _tables = tables;
        _reservationTables = reservationTables;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<ReservationDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync<Reservation, ReservationDto>(
            _reservations.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeReservations(q, query),
            async entities =>
            {
                var list = entities.ToList();
                var ids = list.Select(r => r.Id).ToList();
                var tableIdsByReservation = await LoadTableIdsByReservationAsync(ids, cancellationToken);
                IReadOnlyList<ReservationDto> dtos = list
                    .Select(r =>
                        ToDto(
                            r,
                            tableIdsByReservation.TryGetValue(r.Id, out var tableIds)
                                ? tableIds
                                : Array.Empty<Guid>()))
                    .ToList();
                return dtos;
            },
            cancellationToken);

    public async Task<ReservationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _reservations.Query().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return null;

        var tableIds = await LoadTableIdsForReservationAsync(id, cancellationToken);
        return ToDto(entity, tableIds);
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

        if (dto.DiningTableIds is { Count: > 0 })
        {
            await SyncReservationTablesAsync(entity, dto.DiningTableIds, cancellationToken);
            await ApplyTableStatusesForReservationAsync(entity, previousStatus: null, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await MapDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<ReservationDto?> UpdateAsync(Guid id, UpdateReservationDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _reservations.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        await ValidateCustomerAsync(dto.CustomerId, cancellationToken);
        ValidateWindow(dto.StartAtUtc!.Value, dto.EndAtUtc!.Value);

        var previousStatus = entity.Status;
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

        if (dto.DiningTableIds is not null)
            await SyncReservationTablesAsync(entity, dto.DiningTableIds, cancellationToken);

        await ApplyTableStatusesForReservationAsync(entity, previousStatus, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _reservations.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return false;

        await ReleaseLinkedTablesAsync(id, cancellationToken);
        var links = await _reservationTables.Query().Where(rt => rt.ReservationId == id).ToListAsync(cancellationToken);
        foreach (var link in links)
            _reservationTables.Remove(link);

        _reservations.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<ReservationDto> MapDtoAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var entity = await _reservations.Query().AsNoTracking().FirstAsync(r => r.Id == reservationId, cancellationToken);
        var tableIds = await LoadTableIdsForReservationAsync(reservationId, cancellationToken);
        return ToDto(entity, tableIds);
    }

    private ReservationDto ToDto(Reservation entity, IReadOnlyList<Guid> tableIds)
    {
        var dto = _mapper.Map<ReservationDto>(entity);
        dto.DiningTableIds = tableIds.ToList();
        return dto;
    }

    private async Task<IReadOnlyList<Guid>> LoadTableIdsForReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        await _reservationTables.Query()
            .AsNoTracking()
            .Where(rt => rt.ReservationId == reservationId)
            .Select(rt => rt.DiningTableId)
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<Guid, IReadOnlyList<Guid>>> LoadTableIdsByReservationAsync(
        IReadOnlyList<Guid> reservationIds,
        CancellationToken cancellationToken)
    {
        if (reservationIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<Guid>>();

        var rows = await _reservationTables.Query()
            .AsNoTracking()
            .Where(rt => reservationIds.Contains(rt.ReservationId))
            .Select(rt => new { rt.ReservationId, rt.DiningTableId })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.ReservationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.DiningTableId).ToList());
    }

    private async Task SyncReservationTablesAsync(
        Reservation reservation,
        IReadOnlyList<Guid> tableIds,
        CancellationToken cancellationToken)
    {
        var distinct = tableIds.Distinct().ToList();
        var existing = await _reservationTables.Query()
            .Where(rt => rt.ReservationId == reservation.Id)
            .ToListAsync(cancellationToken);
        var existingIds = existing.Select(e => e.DiningTableId).ToHashSet();

        foreach (var link in existing.Where(e => !distinct.Contains(e.DiningTableId)))
        {
            await SetTableAvailableIfLinkedAsync(link.DiningTableId, cancellationToken);
            _reservationTables.Remove(link);
        }

        var newTableIds = distinct.Where(id => !existingIds.Contains(id)).ToList();
        if (newTableIds.Count == 0)
            return;

        var tablesById = await LoadTablesByIdsAsync(newTableIds, cancellationToken);
        foreach (var tableId in newTableIds)
        {
            if (!tablesById.TryGetValue(tableId, out var table) || !table.IsActive)
                throw new InvalidOperationException("Table was not found or is inactive.");
            if (table.Status == ETableStatus.Busy)
                throw new InvalidOperationException($"Table \"{table.Code}\" is busy and cannot be reserved.");
            if (table.Status == ETableStatus.Reserved)
                throw new InvalidOperationException($"Table \"{table.Code}\" is already reserved.");

            var target = ResolveTargetTableStatus(reservation.Status, previousStatus: null);
            TableStatusTransitions.EnsureCanTransition(table.Status, target);
            table.Status = target;
            _tables.Update(table);

            await _reservationTables.AddAsync(
                new ReservationTable
                {
                    Id = Guid.NewGuid(),
                    ReservationId = reservation.Id,
                    DiningTableId = tableId,
                },
                cancellationToken);
        }
    }

    private async Task ApplyTableStatusesForReservationAsync(
        Reservation reservation,
        ReservationStatus? previousStatus,
        CancellationToken cancellationToken)
    {
        var links = await _reservationTables.Query()
            .Where(rt => rt.ReservationId == reservation.Id)
            .Select(rt => rt.DiningTableId)
            .ToListAsync(cancellationToken);
        if (links.Count == 0)
            return;

        var target = ResolveTargetTableStatus(reservation.Status, previousStatus);
        var tablesById = await LoadTablesByIdsAsync(links, cancellationToken);
        foreach (var tableId in links)
        {
            if (!tablesById.TryGetValue(tableId, out var table))
                continue;

            if (table.Status == target)
                continue;

            TableStatusTransitions.EnsureCanTransition(table.Status, target);
            table.Status = target;
            _tables.Update(table);
        }
    }

    private async Task<Dictionary<Guid, DiningTable>> LoadTablesByIdsAsync(
        IReadOnlyList<Guid> tableIds,
        CancellationToken cancellationToken) =>
        await _tables.Query()
            .Where(t => tableIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

    private async Task ReleaseLinkedTablesAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var tableIds = await _reservationTables.Query()
            .Where(rt => rt.ReservationId == reservationId)
            .Select(rt => rt.DiningTableId)
            .ToListAsync(cancellationToken);

        foreach (var tableId in tableIds)
            await SetTableAvailableIfLinkedAsync(tableId, cancellationToken);
    }

    private async Task SetTableAvailableIfLinkedAsync(Guid tableId, CancellationToken cancellationToken)
    {
        var table = await _tables.GetByIdAsync(tableId, cancellationToken);
        if (table is null || table.Status == ETableStatus.Available)
            return;

        TableStatusTransitions.EnsureCanTransition(table.Status, ETableStatus.Available);
        table.Status = ETableStatus.Available;
        _tables.Update(table);
    }

    private static ETableStatus ResolveTargetTableStatus(ReservationStatus status, ReservationStatus? previousStatus)
    {
        if (status == ReservationStatus.Seated)
            return ETableStatus.Busy;

        if (TableStatusTransitions.IsTerminalReservationRelease(status))
            return ETableStatus.Available;

        if (status is ReservationStatus.Pending or ReservationStatus.Confirmed)
        {
            if (previousStatus == ReservationStatus.Seated)
                return ETableStatus.Available;
            return ETableStatus.Reserved;
        }

        return ETableStatus.Available;
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
