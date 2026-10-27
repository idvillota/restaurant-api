using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Crm.Customers;

public sealed class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TaxId { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
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

    [Range(1, 12)]
    public int? BirthMonth { get; set; }

    [Range(1, 31)]
    public int? BirthDay { get; set; }
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

    [Range(1, 12)]
    public int? BirthMonth { get; set; }

    [Range(1, 31)]
    public int? BirthDay { get; set; }

    public bool IsActive { get; set; } = true;
}

public static class CustomerBirthday
{
    /// <summary>Month+day only. Uses a leap year so 29 Feb is allowed. Both null or both set.</summary>
    public static void Normalize(ref int? month, ref int? day)
    {
        if (month is null && day is null)
            return;

        if (month is null || day is null)
            throw new InvalidOperationException("Indique mes y día de cumpleaños, o deje ambos vacíos.");

        if (month is < 1 or > 12)
            throw new InvalidOperationException("El mes de cumpleaños no es válido.");

        var maxDay = DateTime.DaysInMonth(2000, month.Value);
        if (day < 1 || day > maxDay)
            throw new InvalidOperationException("El día de cumpleaños no es válido para ese mes.");
    }
}
