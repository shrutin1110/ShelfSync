// We need this using because OrderStatus is defined in ShelfSync.Shared.Enums
using ShelfSync.Shared.Enums;

namespace ShelfSync.Shared.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Multi-tenancy
    public Guid TenantId { get; set; }

    // Which user placed this order
    public Guid UserId { get; set; }

    // OrderStatus is an enum: Pending, Confirmed, Processing, Shipped, Delivered, Cancelled
    // Using an enum means you can't accidentally set Status = "Shiped" (typo)
    // The compiler catches invalid values
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // UpdatedAt tracks when status last changed — useful for audit
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    // One order has at most one invoice — that's why it's not ICollection
    public Invoice? Invoice { get; set; }
}