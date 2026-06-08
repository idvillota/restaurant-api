namespace Restaurant.Application.Authorization;

public static class FeatureAuthorizationPolicies
{
    public static string For(string featureCode) => $"Feature:{featureCode}";
}
