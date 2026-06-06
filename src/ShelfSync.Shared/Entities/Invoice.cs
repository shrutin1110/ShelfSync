namespace ShelfSync.Shared.Entities;

// An invoice is a PDF document generated after an order is confirmed
// The PDF is stored in AWS S3, we only keep the file path here
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // One invoice belongs to exactly one order
    public Guid OrderId { get; set; }

    // The path to the PDF file in S3
    // e.g. "invoices/tenant-id/order-id.pdf"
    public string S3Key { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Order Order { get; set; } = null!;
}