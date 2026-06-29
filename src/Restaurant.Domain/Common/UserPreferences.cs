namespace Restaurant.Domain.Common;

public static class UserPreferences
{
    public const string BrandThemeWarm = "warm";
    public const string BrandThemeRefined = "refined";
    public const string BrandThemeOperations = "operations";
    public const string DefaultBrandTheme = BrandThemeOperations;

    public const string ColorSchemeLight = "light";
    public const string ColorSchemeDark = "dark";
    public const string ColorSchemeAuto = "auto";
    public const string DefaultColorScheme = ColorSchemeAuto;

    private static readonly HashSet<string> BrandThemes = new(StringComparer.Ordinal)
    {
        BrandThemeWarm,
        BrandThemeRefined,
        BrandThemeOperations,
    };

    private static readonly HashSet<string> ColorSchemes = new(StringComparer.Ordinal)
    {
        ColorSchemeLight,
        ColorSchemeDark,
        ColorSchemeAuto,
    };

    public static string NormalizeBrandTheme(string? value) =>
        value is not null && BrandThemes.Contains(value) ? value : DefaultBrandTheme;

    public static string NormalizeColorScheme(string? value) =>
        value is not null && ColorSchemes.Contains(value) ? value : DefaultColorScheme;

    public static bool IsValidBrandTheme(string? value) =>
        value is not null && BrandThemes.Contains(value);

    public static bool IsValidColorScheme(string? value) =>
        value is not null && ColorSchemes.Contains(value);
}
