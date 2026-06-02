namespace Restaurant.Application.Features.Reports;

public sealed class StrategicReportDto
{
    public required string Html { get; set; }
    public bool FromCache { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public DateOnly SalesStartDate { get; set; }
    public DateOnly SalesEndDate { get; set; }
}
