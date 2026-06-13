namespace ShelfSync.Orders.Events;

// Published when order status changes to Shipped
// Notification service sends tracking email
public record OrderShippedEvent(
    Guid EventId,
    Guid OrderId,
    Guid TenantId,
    Guid UserId,
    DateTime ShippedAt
);