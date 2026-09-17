namespace Restaurant.Application.Features.Sales.SalesOrders;

/// <summary>Stable cancel reason codes for order / line anulación (logical delete).</summary>
public static class SalesOrderCancelReasons
{
    public const string OrderMistake = "order_mistake";
    public const string GuestCancelled = "guest_cancelled";
    public const string Unavailable = "unavailable";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All =
    [
        OrderMistake,
        GuestCancelled,
        Unavailable,
        Other,
    ];

    public static string ToDisplayLabel(string code, string? detail = null) =>
        code switch
        {
            OrderMistake => "Error de toma",
            GuestCancelled => "Cliente canceló",
            Unavailable => "Producto no disponible",
            Other => string.IsNullOrWhiteSpace(detail) ? "Otro" : $"Otro: {detail.Trim()}",
            _ => string.IsNullOrWhiteSpace(detail) ? code : detail.Trim(),
        };

    public static bool IsKnown(string code) =>
        All.Contains(code, StringComparer.OrdinalIgnoreCase);
}
