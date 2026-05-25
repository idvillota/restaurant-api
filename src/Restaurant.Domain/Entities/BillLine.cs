using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class BillLine : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid BillId { get; set; }
    public Guid? SalesOrderLineId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductTypeName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal ImpoconsumoAmount { get; set; }
    public string? Notes { get; set; }

    public Bill Bill { get; set; } = null!;
}
