namespace ShelfSync.Orders.Events;

// This is the message published to SQS when an order is placed
// Every consumer (Notifications, Warehouse, Lambda) receives this
// and uses whatever fields it needs
public record OrderCreatedEvent(
    // unique ID for this event
    // used for idempotency checking
    Guid EventId,

    // the order that was just created
    Guid OrderId,

    // which tenant placed the order
    Guid TenantId,

    // who placed the order
    Guid UserId,

    // order financial details
    decimal TotalAmount,

    // when the order was placed
    DateTime CreatedAt,

    // the items in the order
    List<OrderCreatedEventItem> Items
);

// One line item in the order
public record OrderCreatedEventItem(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);