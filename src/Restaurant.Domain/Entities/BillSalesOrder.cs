using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class BillSalesOrder : ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid BillId { get; set; }
    public Guid SalesOrderId { get; set; }

    public Bill Bill { get; set; } = null!;
    public SalesOrder SalesOrder { get; set; } = null!;
}
