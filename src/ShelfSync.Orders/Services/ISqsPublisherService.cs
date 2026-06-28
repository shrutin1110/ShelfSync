namespace ShelfSync.Orders.Services;

// Defines what events the Orders service can publish
public interface ISqsPublisher
{
    // Publish event when order is created
    Task PublishOrderCreatedAsync(Events.OrderCreatedEvent orderEvent);

    // Publish event when order is shipped
    Task PublishOrderShippedAsync(Events.OrderShippedEvent shippedEvent);

    // Publish event to generate invoice PDF
    Task PublishInvoiceGenerateAsync(
        Guid orderId,
        Guid tenantId,
        string tenantName,
        decimal totalAmount,
        DateTime createdAt,
        string? notes,
        List<InvoiceItemDto> items);
}
