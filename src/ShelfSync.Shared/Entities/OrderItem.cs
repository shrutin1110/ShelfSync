namespace ShelfSync.Shared.Entities;

// An OrderItem is one line in an order
// If you order 2 red shirts and 1 blue hat, that's 2 OrderItems
public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Which order this line belongs to
    public Guid OrderId { get; set; }

    // Which product was ordered
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    // UnitPrice is a SNAPSHOT of the price at the time of ordering
    // Because the product price might change tomorrow
    // You need to know what the customer actually paid
    public decimal UnitPrice { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}