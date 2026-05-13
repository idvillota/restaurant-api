using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Organization.Employees;

public sealed class EmployeeDto
{
    public Guid Id { get; set; }
    public Guid? TenantUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public DateOnly? HiredOn { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateEmployeeDto
{
    public Guid? TenantUserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? JobTitle { get; set; }

    public DateOnly? HiredOn { get; set; }
}

public sealed class UpdateEmployeeDto
{
    public Guid? TenantUserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? JobTitle { get; set; }

    public DateOnly? HiredOn { get; set; }

    public bool IsActive { get; set; } = true;
}
