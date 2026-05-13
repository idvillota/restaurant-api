using Restaurant.Application.Features.Reservations;

namespace Restaurant.Application.Common.Interfaces;

public interface IReservationService
{
    Task<IReadOnlyList<ReservationDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ReservationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReservationDto> CreateAsync(CreateReservationDto dto, CancellationToken cancellationToken = default);
    Task<ReservationDto?> UpdateAsync(Guid id, UpdateReservationDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
