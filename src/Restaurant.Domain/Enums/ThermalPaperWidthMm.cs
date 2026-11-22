namespace Restaurant.Domain.Enums;

/// <summary>
/// Thermal roll width for kitchen tickets and sales receipts (PDF + print XML).
/// </summary>
public enum ThermalPaperWidthMm
{
    /// <summary>Narrow roll (~58 mm). Common “60 mm” stock.</summary>
    Mm58 = 58,

    /// <summary>Wide roll (80 mm).</summary>
    Mm80 = 80,
}

public static class ThermalPaperWidth
{
    public static ThermalPaperWidthMm Normalize(int raw) =>
        raw == (int)ThermalPaperWidthMm.Mm80
            ? ThermalPaperWidthMm.Mm80
            : ThermalPaperWidthMm.Mm58;

    public static float ToMillimetres(ThermalPaperWidthMm width) => (float)(int)width;

    public static bool IsNarrow(ThermalPaperWidthMm width) => width != ThermalPaperWidthMm.Mm80;
}
