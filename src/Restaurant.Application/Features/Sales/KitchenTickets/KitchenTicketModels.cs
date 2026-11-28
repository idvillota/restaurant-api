namespace Restaurant.Application.Features.Sales.KitchenTickets;

public sealed class KitchenTicketLineModel
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductTypeId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
    public List<string> ExcludedIngredientNames { get; set; } = [];
}

public sealed class KitchenTicketModel
{
    public string TableCode { get; set; } = string.Empty;
    public bool IsDelivery { get; set; }
    public bool IsCounterPickup { get; set; }
    public string? CounterCustomerName { get; set; }
    public string? CounterPhone { get; set; }
    public string? CounterPagerNumber { get; set; }

    /// <summary>Address captured on the delivery dialog, including a one-time address.</summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>Phone captured on the delivery dialog.</summary>
    public string? DeliveryPhone { get; set; }

    /// <summary>
    /// One line for the comanda: "address - phone". Either part is omitted when blank.
    /// </summary>
    public static string? FormatDeliveryDestination(string? address, string? phone)
    {
        var trimmedAddress = address?.Trim();
        var trimmedPhone = phone?.Trim();
        var hasAddress = !string.IsNullOrEmpty(trimmedAddress);
        var hasPhone = !string.IsNullOrEmpty(trimmedPhone);
        if (hasAddress && hasPhone)
            return $"{trimmedAddress} - {trimmedPhone}";
        if (hasAddress)
            return trimmedAddress;
        if (hasPhone)
            return trimmedPhone;
        return null;
    }
    public string OrderNumber { get; set; } = string.Empty;
    public string SentBy { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public string? PrinterStationName { get; set; }
    public string? PrinterStationCode { get; set; }
    public List<KitchenTicketLineModel> Lines { get; set; } = [];

    /// <summary>When true, ticket is an anulación (cancel) comanda rather than a send.</summary>
    public bool IsCancellation { get; set; }

    /// <summary>Human-readable cancel reason printed on anulación tickets.</summary>
    public string? CancelReason { get; set; }

    /// <summary>Thermal paper width in mm (58 or 80). Used by PDF and print XML.</summary>
    public int PaperWidthMm { get; set; } = 58;
}
