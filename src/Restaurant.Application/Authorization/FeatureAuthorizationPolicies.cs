namespace Restaurant.Application.Authorization;

public static class FeatureAuthorizationPolicies
{
    public const string SalonCatalogProductsRead = "Feature:SalonCatalogProductsRead";
    public const string SalonCatalogProductTypesRead = "Feature:SalonCatalogProductTypesRead";
    public const string OperationalCashierContextRead = "Feature:OperationalCashierContextRead";

    public static string For(string featureCode) => $"Feature:{featureCode}";
}
