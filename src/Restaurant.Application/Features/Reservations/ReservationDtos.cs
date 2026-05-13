using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Reservations;

public sealed class ReservationDto
{
    public Guid Id { get; set; }
    public Guid? CustomerId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public int PartySize { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public ReservationStatus Status { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateReservationDto
{
    public Guid? CustomerId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(320)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    [MaxLength(40)]
    public string? ContactPhone { get; set; }

    [Range(1, 500)]
    public int PartySize { get; set; }

    [Required]
    public DateTime? StartAtUtc { get; set; }

    [Required]
    public DateTime? EndAtUtc { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public sealed class UpdateReservationDto
{
    public Guid? CustomerId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(320)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    [MaxLength(40)]
    public string? ContactPhone { get; set; }

    [Range(1, 500)]
    public int PartySize { get; set; }

    [Required]
    public DateTime? StartAtUtc { get; set; }

    [Required]
    public DateTime? EndAtUtc { get; set; }

    public ReservationStatus Status { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
