using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class PrintJob : EntityBase, ITenantScoped
{
    public const string StatusPending = "Pending";
    public const string StatusPrinted = "Printed";

    public Guid TenantId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string PayloadFormat { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = StatusPending;
    public string? ErrorMessage { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
}
