using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Data;
using ShelfSync.Orders.DTOs;
using ShelfSync.Shared.Entities;
using ShelfSync.Shared.Enums;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Orders.GraphQL.Mutations;

[MutationType]
public class OrderMutation
{
    // placeOrder mutation
    // Client sends: { items: [...], notes: "..." }
    // Server creates order and returns result
    public async Task<PlaceOrderResult> PlaceOrder(
        PlaceOrderInput input,
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        // Validate input — must have at least one item
        if (input.Items is null || !input.Items.Any())
        {
            return new PlaceOrderResult(
                Success: false,
                ErrorMessage: "Order must have at least one item.",
                OrderId: null);
        }

        // Calculate total and verify products exist
        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();

        foreach (var item in input.Items)
        {
            // Verify product exists and belongs to this tenant
            var product = await db.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == item.ProductId &&
                    p.TenantId == tenantContext.TenantId &&
                    p.IsActive);

            if (product is null)
            {
                return new PlaceOrderResult(
                    Success: false,
                    ErrorMessage: $"Product {item.ProductId} not found.",
                    OrderId: null);
            }

            // Check stock availability
            if (product.StockQuantity < item.Quantity)
            {
                return new PlaceOrderResult(
                    Success: false,
                    ErrorMessage: $"Insufficient stock for {product.Name}.",
                    OrderId: null);
            }

            // Snapshot the price at time of order
            // Never use current price — it might change tomorrow
            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price // snapshot
            };

            orderItems.Add(orderItem);
            totalAmount += product.Price * item.Quantity;

            // Reduce stock
            product.StockQuantity -= item.Quantity;
        }

        // Create the order
        var order = new Order
        {
            TenantId = tenantContext.TenantId,

            // For now use a placeholder UserId
            // On Day 14 when React sends JWT
            // we will extract real UserId from token
            UserId = Guid.Empty,

            Status = OrderStatus.Pending,
            TotalAmount = totalAmount,
            Notes = input.Notes,
            Items = orderItems
        };

        db.Orders.Add(order);

        // SaveChangesAsync saves BOTH the order
        // AND the stock reduction in one transaction
        // Either both succeed or neither does
        await db.SaveChangesAsync();

        
        return new PlaceOrderResult(
            Success: true,
            ErrorMessage: null,
            OrderId: order.Id);
    }

    // updateOrderStatus mutation
    // Changes order status with state machine validation
    public async Task<Order?> UpdateOrderStatus(
        UpdateOrderStatusInput input,
        OrdersDbContext db,
        ITenantContext tenantContext,
       [Service] ITopicEventSender eventSender)
    {
        var order = await db.Orders
            .FirstOrDefaultAsync(o =>
                o.Id == input.OrderId &&
                o.TenantId == tenantContext.TenantId);

        if (order is null) return null;

        // State machine validation
        // Not every status transition is valid
        // e.g. cannot go from Delivered back to Pending
        var validTransitions = new Dictionary
            <OrderStatus, List<OrderStatus>>
        {
            // From Pending you can go to Confirmed or Cancelled
            { OrderStatus.Pending,
              new() { OrderStatus.Confirmed,
                      OrderStatus.Cancelled } },

            // From Confirmed you can go to Processing or Cancelled
            { OrderStatus.Confirmed,
              new() { OrderStatus.Processing,
                      OrderStatus.Cancelled } },

            // From Processing you can only go to Shipped
            { OrderStatus.Processing,
              new() { OrderStatus.Shipped } },

            // From Shipped you can only go to Delivered
            { OrderStatus.Shipped,
              new() { OrderStatus.Delivered } },

            // Terminal states — no transitions allowed
            { OrderStatus.Delivered, new() { } },
            { OrderStatus.Cancelled, new() { } }
        };

        // Check if this transition is valid
        if (!validTransitions[order.Status]
                .Contains(input.NewStatus))
        {
            // Return null to indicate invalid transition
            // In a production app you would throw a GraphQL error
            return null;
        }

        order.Status = input.NewStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        
        // Publish event to the "OrderStatusChanged" topic
        // Every client subscribed to this topic receives the update
        await eventSender.SendAsync(
            "OrderStatusChanged",
            order);


        return order;
    }

    // addProduct mutation
    public async Task<Product> AddProduct(
        AddProductInput input,
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        var product = new Product
        {
            TenantId = tenantContext.TenantId,
            Name = input.Name,
            SKU = input.SKU,
            Price = input.Price,
            StockQuantity = input.InitialStock,
            IsActive = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product;
    }
}