namespace Restaurant.Application.Features.Sales.LunchTickets;

public sealed class LunchTicketBalanceDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int RemainingTickets { get; set; }
    public int TotalTicketsPurchased { get; set; }
    public int UsedTickets { get; set; }
    public IReadOnlyList<LunchTicketBookletSummaryDto> Booklets { get; set; } = [];
}

public sealed class LunchTicketBookletSummaryDto
{
    public Guid Id { get; set; }
    public string PackProductName { get; set; } = string.Empty;
    public int TotalTickets { get; set; }
    public int RemainingTickets { get; set; }
    public int UsedTickets { get; set; }
    public DateTime PurchasedAtUtc { get; set; }
}
