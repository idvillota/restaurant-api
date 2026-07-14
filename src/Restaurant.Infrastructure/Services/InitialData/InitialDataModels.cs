namespace Restaurant.Infrastructure.Services.InitialData;

internal static class InitialDataSheetNames
{
    public const string Tenant = "Tenant";
    public const string Billing = "Billing";
    public const string ProductTypes = "ProductTypes";
    public const string Products = "Products";
    public const string Ingredients = "Ingredients";
    public const string Recipes = "Recipes";
    public const string DiningTables = "DiningTables";

    public static readonly string[] Required =
    [
        Tenant,
        Billing,
        ProductTypes,
        Products,
        Ingredients,
        Recipes,
        DiningTables,
    ];
}

internal sealed class InitialDataWorkbook
{
    public TenantRow? Tenant { get; set; }
    public BillingRow? Billing { get; set; }
    public List<ProductTypeRow> ProductTypes { get; } = [];
    public List<ProductRow> Products { get; } = [];
    public List<IngredientRow> Ingredients { get; } = [];
    public List<RecipeRow> Recipes { get; } = [];
    public List<DiningTableRow> DiningTables { get; } = [];
}

internal sealed class TenantRow
{
    public int RowNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? TimeZoneId { get; init; }
    public string CurrencyCode { get; init; } = "COP";
}

internal sealed class BillingRow
{
    public int RowNumber { get; init; }
    public string TradeName { get; init; } = string.Empty;
    public string LegalName { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public string TaxRegime { get; init; } = "Régimen Simplificado";
    public string? LegalRepresentative { get; init; }
    public string AddressLine { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = "Colombia";
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
    public decimal MaxDiscountPercent { get; init; } = 15m;
    public int OperationalDayCutoffHour { get; init; } = 4;
    public decimal ImpoconsumoPercent { get; init; } = 8m;
}

internal sealed class ProductTypeRow
{
    public int RowNumber { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
}

internal sealed class ProductRow
{
    public int RowNumber { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ProductTypeCode { get; init; } = string.Empty;
    public string CompositionType { get; init; } = "Prepared";
    public decimal UnitPrice { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}

internal sealed class IngredientRow
{
    public int RowNumber { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Unit { get; init; } = "Unit";
    public decimal? UnitCost { get; init; }
    public decimal? StockQuantity { get; init; }
    public decimal? ReorderLevel { get; init; }
    public bool IsActive { get; init; } = true;
}

internal sealed class RecipeRow
{
    public int RowNumber { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string IngredientCode { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}

internal sealed class DiningTableRow
{
    public int RowNumber { get; init; }
    public string Code { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public string? Zone { get; init; }
    public double? LayoutX { get; init; }
    public double? LayoutY { get; init; }
}
