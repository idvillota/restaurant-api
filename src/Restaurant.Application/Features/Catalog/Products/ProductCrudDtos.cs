using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Catalog.Products;

public sealed class CreateProductDto
{
    [Required]
    public Guid ProductTypeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Sku { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}

public sealed class UpdateProductDto
{
    [Required]
    public Guid ProductTypeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Sku { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; } = true;
}
