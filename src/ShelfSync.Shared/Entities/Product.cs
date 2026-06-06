namespace ShelfSync.Shared.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Multi-tenancy — product belongs to one tenant
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    // SKU = Stock Keeping Unit — a unique product code like "TSHIRT-RED-L"
    public string SKU { get; set; } = string.Empty;
    
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    // When a product image is uploaded to S3, we store the file path here
    // not the full URL — the URL can be constructed from the key
    // ? means this is optional (product might not have an image yet)
    public string? S3ImageKey { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}