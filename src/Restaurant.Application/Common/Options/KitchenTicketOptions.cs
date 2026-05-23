namespace Restaurant.Application.Common.Options;

public sealed class KitchenTicketOptions
{
    public const string SectionName = "KitchenTickets";

    /// <summary>Relative to API content root. PDFs are stored under orders/files-without-print.</summary>
    public string RootPath { get; set; } = "orders/files-without-print";
}
