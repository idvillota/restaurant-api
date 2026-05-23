using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

/// <summary>Global permission catalog (same codes for every tenant).</summary>
public class Feature : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<RoleFeature> RoleFeatures { get; set; } = new List<RoleFeature>();
}
