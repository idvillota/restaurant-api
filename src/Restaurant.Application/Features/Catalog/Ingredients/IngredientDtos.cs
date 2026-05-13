using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Catalog.Ingredients;

public sealed class IngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IngredientUnit Unit { get; set; }
    public decimal? StockQuantity { get; set; }
    public decimal? ReorderLevel { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateIngredientDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public IngredientUnit? Unit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? StockQuantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ReorderLevel { get; set; }
}

public sealed class UpdateIngredientDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public IngredientUnit? Unit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? StockQuantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;
}
