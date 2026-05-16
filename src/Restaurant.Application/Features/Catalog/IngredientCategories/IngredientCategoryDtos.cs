using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Catalog.IngredientCategories;

public sealed class IngredientCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateIngredientCategoryDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
}

public sealed class UpdateIngredientCategoryDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
