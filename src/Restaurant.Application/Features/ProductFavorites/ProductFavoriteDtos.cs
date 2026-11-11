namespace Restaurant.Application.Features.ProductFavorites;

public sealed class ProductFavoriteListDto
{
    public IReadOnlyList<Guid> ProductIds { get; set; } = Array.Empty<Guid>();
    public int MaxCount { get; set; } = 10;
}
