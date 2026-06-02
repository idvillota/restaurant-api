using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Catalog.Products;

public sealed class ProductBundleDto
{
    public Guid ProductId { get; set; }
    public EProductType CompositionType { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }
    public IReadOnlyList<ProductBundleLineDto> Lines { get; set; } = Array.Empty<ProductBundleLineDto>();
}

public sealed class ProductBundleLineDto
{
    public Guid Id { get; set; }
    public Guid ComponentProductId { get; set; }
    public string ComponentProductName { get; set; } = string.Empty;
    public EProductType ComponentCompositionType { get; set; }
    public decimal ComponentUnitPrice { get; set; }
    public decimal ComponentCostPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal LineCost { get; set; }
    public int SortOrder { get; set; }
}

public sealed class SetProductBundleDto
{
    public List<SetProductBundleLineDto> Lines { get; set; } = [];
}

public sealed class SetProductBundleLineDto
{
    [Required]
    public Guid ComponentProductId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; } = 1m;

    public int SortOrder { get; set; }
}
