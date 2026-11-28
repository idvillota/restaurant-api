using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class SalesOrderLineChoice : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SalesOrderLineId { get; set; }
    public Guid ComboGroupId { get; set; }
    public Guid ProductId { get; set; }
    public int SortOrder { get; set; }

    public SalesOrderLine SalesOrderLine { get; set; } = null!;
    public ComboGroup Group { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
