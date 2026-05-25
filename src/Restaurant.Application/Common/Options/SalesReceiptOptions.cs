namespace Restaurant.Application.Common.Options;

public sealed class SalesReceiptOptions
{
    public const string SectionName = "SalesReceipts";

    public string RootPath { get; set; } = "orders/sales-receipts";
}
