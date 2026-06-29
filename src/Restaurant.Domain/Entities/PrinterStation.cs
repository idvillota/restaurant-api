using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class PrinterStation : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<ProductTypePrinterMapping> ProductTypeMappings { get; set; } =
        new List<ProductTypePrinterMapping>();
}
