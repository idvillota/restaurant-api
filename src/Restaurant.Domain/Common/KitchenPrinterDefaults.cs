namespace Restaurant.Domain.Common;

public static class KitchenPrinterDefaults
{
    public const string DefaultStationCode = "DEFAULT";
    public const string DefaultStationName = "Cocina (predeterminada)";

    /// <summary>Sufijo de facturas PDF que el agente de PC enruta a caja.</summary>
    public const string ReceiptStationCode = "RECEIPT";

    public const string ReceiptStationName = "Facturas";

    public static bool IsReservedStationCode(string? code) =>
        string.Equals(code, DefaultStationCode, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(code, ReceiptStationCode, StringComparison.OrdinalIgnoreCase);
}
