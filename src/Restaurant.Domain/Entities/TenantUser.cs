using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class TenantUser : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public string BrandTheme { get; set; } = UserPreferences.DefaultBrandTheme;
    public string ColorScheme { get; set; } = UserPreferences.DefaultColorScheme;

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<TenantUserRole> TenantUserRoles { get; set; } = new List<TenantUserRole>();
    public Employee? Employee { get; set; }
}
