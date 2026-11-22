using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Inventory.ProductProductions;

public sealed class ProductProductionLineDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal StockQuantitySnapshot { get; set; }
}

public class ProductProductionDocumentListItemDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByUserEmail { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}

public sealed class ProductProductionDocumentDto : ProductProductionDocumentListItemDto
{
    public IReadOnlyList<ProductProductionLineDto> Lines { get; set; } = [];
}

public sealed class CreateProductProductionLineDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }
}

public sealed class CreateProductProductionDocumentDto
{
    /// <summary>Optional. When blank, the API assigns PROD-yyyyMMdd-0001 (Colombia day + consecutive).</summary>
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MinLength(1)]
    public List<CreateProductProductionLineDto> Lines { get; set; } = [];
}
