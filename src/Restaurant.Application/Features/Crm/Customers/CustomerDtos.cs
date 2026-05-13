using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Crm.Customers;

public sealed class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TaxId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateCustomerDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(320)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(80)]
    public string? TaxId { get; set; }
}

public sealed class UpdateCustomerDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(320)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(80)]
    public string? TaxId { get; set; }

    public bool IsActive { get; set; } = true;
}
