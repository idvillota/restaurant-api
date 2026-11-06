namespace Restaurant.Domain.Sales;

/// <summary>
/// Report-only: bill lines whose product type name is Vitrina Café.
/// Selling and cash collected are unchanged; cierre lists these apart from general ventas.
/// </summary>
public static class VitrinaCafeProductType
{
    public const string Name = "Vitrina Café";

    public static bool Matches(string? productTypeName) =>
        !string.IsNullOrWhiteSpace(productTypeName)
        && productTypeName.Trim().Equals(Name, StringComparison.OrdinalIgnoreCase);

    public static (decimal GeneralSales, decimal VitrinaCafeSales) SplitCollected(
        decimal collectedPayments,
        decimal vitrinaLineTotal)
    {
        var vitrina = decimal.Round(Math.Max(0, vitrinaLineTotal), 2, MidpointRounding.AwayFromZero);
        var general = decimal.Round(collectedPayments - vitrina, 2, MidpointRounding.AwayFromZero);
        if (general < 0)
            general = 0;
        return (general, vitrina);
    }
}
