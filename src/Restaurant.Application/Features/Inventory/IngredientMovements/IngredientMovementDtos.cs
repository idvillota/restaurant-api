using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Inventory.IngredientMovements;

public sealed class IngredientMovementLineDto
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? StockQuantitySnapshot { get; set; }
    public decimal? UnitCostSnapshot { get; set; }
}

public class IngredientMovementDocumentListItemDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public Guid IngredientMovementTypeId { get; set; }
    public string MovementTypeName { get; set; } = string.Empty;
    public bool IsInput { get; set; }
    public int LineCount { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByUserEmail { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}

public sealed class IngredientMovementDocumentDto : IngredientMovementDocumentListItemDto
{
    public IReadOnlyList<IngredientMovementLineDto> Lines { get; set; } = [];
}

public sealed class CreateIngredientMovementLineDto
{
    [Required]
    public Guid IngredientId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }
}

public sealed class CreateIngredientMovementDocumentDto
{
    [Required]
    public Guid IngredientMovementTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MinLength(1)]
    public List<CreateIngredientMovementLineDto> Lines { get; set; } = [];
}
