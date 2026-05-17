using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Catalog.Products;

public sealed class CreateProductDto
{
    public EProductType CompositionType { get; set; } = EProductType.Prepared;

    [Required]
    public Guid ProductTypeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(80)]
    public string? Sku { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    /// <summary>Required when <see cref="CompositionType"/> is <see cref="EProductType.Resale"/>.</summary>
    public Guid? ResaleIngredientId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal ResaleQuantity { get; set; } = 1m;
}

public sealed class UpdateProductDto
{
    public EProductType CompositionType { get; set; } = EProductType.Prepared;

    [Required]
    public Guid ProductTypeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(80)]
    public string? Sku { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Required when <see cref="CompositionType"/> is <see cref="EProductType.Resale"/>.</summary>
    public Guid? ResaleIngredientId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal ResaleQuantity { get; set; } = 1m;
}
