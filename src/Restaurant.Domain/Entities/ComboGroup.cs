using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class ComboGroup : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    /// <summary>
    /// Hidden from new orders. Kept so past sales can still name the topic they used.
    /// </summary>
    public bool IsRetired { get; set; }

    public Product Product { get; set; } = null!;
    public ICollection<ComboGroupOption> Options { get; set; } = new List<ComboGroupOption>();
}
