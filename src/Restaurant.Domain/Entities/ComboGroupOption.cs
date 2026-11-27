using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class ComboGroupOption : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ComboGroupId { get; set; }
    public Guid ProductId { get; set; }
    public int SortOrder { get; set; }

    public ComboGroup ComboGroup { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
