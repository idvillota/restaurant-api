using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Procurement.Providers;

public sealed class ProviderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateProviderDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(80)]
    public string? TaxId { get; set; }

    [MaxLength(320)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public sealed class UpdateProviderDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(80)]
    public string? TaxId { get; set; }

    [MaxLength(320)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
