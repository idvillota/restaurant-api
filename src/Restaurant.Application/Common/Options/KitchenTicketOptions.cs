namespace Restaurant.Application.Common.Options;

public sealed class KitchenTicketOptions
{
    public const string SectionName = "KitchenTickets";

    /// <summary>Relative to API content root. Comanda PDFs are stored under files/orders.</summary>
    public string RootPath { get; set; } = "files/orders";
}
