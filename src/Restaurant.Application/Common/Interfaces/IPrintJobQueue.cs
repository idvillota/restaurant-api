namespace Restaurant.Application.Common.Interfaces;

public interface IPrintJobQueue
{
    Task EnqueueAsync(
        string kind,
        string payloadFormat,
        string payload,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrintJobTicket>> ListPendingAsync(CancellationToken cancellationToken = default);

    Task<bool> AcknowledgeAsync(
        Guid jobId,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default);
}

public sealed record PrintJobTicket(Guid Id, string Kind, string PayloadFormat, string Payload);

public static class PrintJobFormats
{
    public const string SalesReceiptXml = "sales-receipt-xml";
    public const string KitchenTicketXml = "kitchen-ticket-xml";
}

public static class PrintJobKinds
{
    public const string Receipt = "receipt";
    public const string Kitchen = "kitchen";
}
