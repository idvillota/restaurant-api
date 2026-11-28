using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class SalesOrder : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? DiningTableId { get; set; }
    public Guid? CustomerId { get; set; }
    public bool IsDelivery { get; set; }
    public string? DeliveryCustomerName { get; set; }
    public string? DeliveryPhone { get; set; }
    public string? DeliveryAddress { get; set; }

    /// <summary>Food-plaza pickup. No dining table. Paid before the kitchen starts.</summary>
    public bool IsCounterPickup { get; set; }

    public string? CounterCustomerName { get; set; }
    public string? CounterPhone { get; set; }

    /// <summary>Copied from the tenant when the order opens. Listo does not send WhatsApp when this is true.</summary>
    public bool CounterUsesPager { get; set; }

    /// <summary>Puck number for this order. Empty when the guest is called by WhatsApp.</summary>
    public string? CounterPagerNumber { get; set; }

    /// <summary>Set when the whole order is ready for pickup. Null until staff presses Listo.</summary>
    public DateTime? CounterReadyAtUtc { get; set; }

    /// <summary>Set only after the ready notice was accepted by the provider.</summary>
    public DateTime? CounterNotifiedAtUtc { get; set; }

    /// <summary>Last ready-notice failure. The order can still be ready and handed over.</summary>
    public string? CounterNotificationError { get; set; }

    public DateTime? CounterHandedOverAtUtc { get; set; }

    /// <summary>Paid order nobody collected. The sale stays; the ticket leaves the counter queue.</summary>
    public DateTime? CounterAbandonedAtUtc { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>User who logically voided the order (Status = Voided).</summary>
    public Guid? VoidedByUserId { get; set; }

    /// <summary>Reason code/label stored when the order was voided.</summary>
    public string? VoidReason { get; set; }

    public DiningTable? DiningTable { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
