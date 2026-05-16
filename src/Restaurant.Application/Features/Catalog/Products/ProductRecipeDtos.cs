using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Catalog.Products;

public sealed class ProductRecipeDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }
    public IReadOnlyList<ProductRecipeLineDto> Lines { get; set; } = Array.Empty<ProductRecipeLineDto>();
}

public sealed class ProductRecipeLineDto
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public Guid IngredientCategoryId { get; set; }
    public string IngredientCategoryName { get; set; } = string.Empty;
    public IngredientUnit Unit { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal Quantity { get; set; }
    public decimal LineCost { get; set; }
}

public sealed class SetProductRecipeDto
{
    public List<SetProductRecipeLineDto> Lines { get; set; } = [];
}

public sealed class SetProductRecipeLineDto
{
    [Required]
    public Guid IngredientId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }
}
