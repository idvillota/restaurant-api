using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

/// <summary>
/// Remembers a tablet batch item so a retry does not append the same lines again.
/// </summary>
public class MobileSyncReceipt : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string LocalOrderId { get; set; } = string.Empty;
    public Guid RemoteOrderId { get; set; }

    /// <summary>Comma-separated ids of the lines this tablet uploaded.</summary>
    public string LineIds { get; set; } = string.Empty;
}
