using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

/// <summary>
/// Prepaid lunch tickets sold as a pack. Revenue is recognized on purchase; redemptions consume tickets at $0.
/// </summary>
public class LunchTicketBooklet : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? PurchaseBillId { get; set; }
    public Guid PackProductId { get; set; }
    public int TotalTickets { get; set; }
    public int RemainingTickets { get; set; }
    public decimal PackUnitPrice { get; set; }
    public DateTime PurchasedAtUtc { get; set; }

    public Customer Customer { get; set; } = null!;
    public Bill? PurchaseBill { get; set; }
    public Product PackProduct { get; set; } = null!;
}
