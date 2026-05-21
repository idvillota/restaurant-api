using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Operations.DiningTables;

public sealed class DiningTableDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Zone { get; set; }
    public double? LayoutX { get; set; }
    public double? LayoutY { get; set; }
    public ETableStatus Status { get; set; }
    public bool IsActive { get; set; }
}

public sealed class DiningTableLayoutItemDto
{
    [Required]
    public Guid TableId { get; set; }

    [Range(0, 100)]
    public double LayoutX { get; set; }

    [Range(0, 100)]
    public double LayoutY { get; set; }
}

public sealed class UpdateDiningTableLayoutsDto
{
    [MinLength(1)]
    public List<DiningTableLayoutItemDto> Items { get; set; } = [];
}

public sealed class SetDiningTableStatusDto
{
    public ETableStatus Status { get; set; }
}

public sealed class CreateDiningTableDto
{
    [Required]
    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Range(1, 500)]
    public int Capacity { get; set; }

    [MaxLength(80)]
    public string? Zone { get; set; }
}

public sealed class UpdateDiningTableDto
{
    [Required]
    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Range(1, 500)]
    public int Capacity { get; set; }

    [MaxLength(80)]
    public string? Zone { get; set; }

    public bool IsActive { get; set; } = true;
}
