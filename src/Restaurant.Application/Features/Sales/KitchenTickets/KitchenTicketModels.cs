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
    public IReadOnlyList<KitchenTicketLineModel> Lines { get; set; } = [];
}
