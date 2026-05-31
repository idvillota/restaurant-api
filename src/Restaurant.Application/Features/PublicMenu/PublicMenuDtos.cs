namespace Restaurant.Application.Features.PublicMenu;

public sealed class PublicMenuDto
{
    public string TenantName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "COP";
    public IReadOnlyList<PublicMenuCategoryDto> Categories { get; set; } = [];
}

public sealed class PublicMenuCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public IReadOnlyList<PublicMenuProductDto> Products { get; set; } = [];
}

public sealed class PublicMenuProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public string? ImageUrl { get; set; }
}
