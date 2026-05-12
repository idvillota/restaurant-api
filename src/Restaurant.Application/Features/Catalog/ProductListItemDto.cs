namespace Restaurant.Application.Features.Catalog;

public sealed class ProductListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid ProductTypeId { get; set; }
    public string ProductTypeName { get; set; } = string.Empty;
}
