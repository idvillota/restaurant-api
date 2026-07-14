namespace Restaurant.Application.Features.Platform;

public sealed class TenantInitialDataErrorDto
{
    public string Sheet { get; init; } = string.Empty;
    public int? Row { get; init; }
    public string? Field { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class TenantInitialDataImportResultDto
{
    public Guid TenantId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> UsersCreated { get; init; } = [];
    public TenantInitialDataCountsDto Counts { get; init; } = new();
}

public sealed class TenantInitialDataCountsDto
{
    public int ProductTypes { get; init; }
    public int Products { get; init; }
    public int IngredientCategories { get; init; }
    public int Ingredients { get; init; }
    public int Recipes { get; init; }
    public int DiningTables { get; init; }
}
