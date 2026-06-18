using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public sealed class StrategicAiReportCache : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public DateOnly SalesStartDate { get; set; }
    public DateOnly SalesEndDate { get; set; }
    /// <summary>UTC calendar day when this cached report was produced (daily cache bucket).</summary>
    public DateOnly CacheDate { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public int? ForecastDays { get; set; }
    public string HtmlContent { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
}
