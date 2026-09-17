namespace Restaurant.Application.Features.Sales.KitchenTickets;

public sealed class KitchenTicketLineModel
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<string> ExcludedIngredientNames { get; set; } = [];
}

public sealed class KitchenTicketModel
{
    public string TableCode { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string SentBy { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public string? PrinterStationName { get; set; }
    public string? PrinterStationCode { get; set; }
    public IReadOnlyList<KitchenTicketLineModel> Lines { get; set; } = [];

    /// <summary>When true, ticket is an anulación (cancel) comanda rather than a send.</summary>
    public bool IsCancellation { get; set; }

    /// <summary>Human-readable cancel reason printed on anulación tickets.</summary>
    public string? CancelReason { get; set; }
}
