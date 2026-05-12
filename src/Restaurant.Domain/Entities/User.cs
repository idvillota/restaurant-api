using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class User : EntityBase
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? DisplayName { get; set; }

    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
}
