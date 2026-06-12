using ShelfSync.Shared.Enums;

namespace ShelfSync.Orders.DTOs;

// Input for placing an order
// "record" = simple immutable data container
public record PlaceOrderInput(
    List<OrderItemInput> Items,
    string? Notes);

// Each item in the order
public record OrderItemInput(
    Guid ProductId,
    int Quantity);

// Input for updating order status
public record UpdateOrderStatusInput(
    Guid OrderId,
    OrderStatus NewStatus);

// Response after placing an order
public record PlaceOrderResult(
    bool Success,
    string? ErrorMessage,
    Guid? OrderId);
// Add to OrderDTOs.cs
public record AddProductInput(
    string Name,
    string SKU,
    decimal Price,
    int InitialStock);