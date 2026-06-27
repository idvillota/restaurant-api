using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class TenantSettings : ITenantScoped
{
    public Guid TenantId { get; set; }
    public decimal MaxDiscountPercent { get; set; } = 10m;
    /// <summary>Hour (0-23) in tenant local time when the operational day rolls over (e.g. 4 = sales until 03:59 belong to previous day).</summary>
    public int OperationalDayCutoffHour { get; set; } = 4;

    /// <summary>When set and ahead of the clock-based operational date, operations use this date (e.g. after daily closure advances to the next day).</summary>
    public DateOnly? ActiveOperationalBusinessDate { get; set; }

    public string TradeName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string TaxRegime { get; set; } = "Régimen Simplificado";
    public string TaxId { get; set; } = string.Empty;
    public string? LegalRepresentative { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = "Colombia";
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? DianResolutionNumber { get; set; }
    public int DianResolutionFrom { get; set; }
    public int DianResolutionTo { get; set; }
    public int DianNextConsecutive { get; set; }
    public string? InvoiceNumberPrefix { get; set; }
    public decimal ImpoconsumoPercent { get; set; } = 8m;

    /// <summary>JSON layout for tenant dashboard panels (react-grid-layout).</summary>
    public string? DashboardLayoutJson { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
