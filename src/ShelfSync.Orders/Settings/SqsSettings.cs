namespace ShelfSync.Orders.Settings;

// Maps to the "SQS" section in appsettings.json
// Each property is the full URL of one SQS queue
public class SqsSettings
{
    public string OrderCreatedQueueUrl { get; set; }
        = string.Empty;

    public string OrderShippedQueueUrl { get; set; }
        = string.Empty;

    public string InvoiceGenerateQueueUrl { get; set; }
        = string.Empty;

    public string InventoryLowQueueUrl { get; set; }
        = string.Empty;
}