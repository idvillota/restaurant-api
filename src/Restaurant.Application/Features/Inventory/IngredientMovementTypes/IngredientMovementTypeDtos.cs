using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Inventory.IngredientMovementTypes;

public sealed class IngredientMovementTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsInput { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateIngredientMovementTypeDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsInput { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpdateIngredientMovementTypeDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsInput { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
