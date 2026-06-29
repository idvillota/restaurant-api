using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class ProductTypePrinterMapping : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductTypeId { get; set; }
    public Guid PrinterStationId { get; set; }

    public ProductType ProductType { get; set; } = null!;
    public PrinterStation PrinterStation { get; set; } = null!;
}
