using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Data;
using ShelfSync.Orders.DTOs;
using ShelfSync.Orders.Services;
using ShelfSync.Shared.Entities;
using ShelfSync.Shared.Enums;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Orders.GraphQL.Mutations;

[MutationType]
public class OrderMutation
{
    public async Task<PlaceOrderResult> PlaceOrder(
    PlaceOrderInput input,
    OrdersDbContext db,
    ITenantContext tenantContext,
    IWarehouseService warehouseService, // ← inject warehouse service
    [Service] IHttpContextAccessor httpContextAccessor)
{
    // Read the real UserId from the JWT token
    var userIdClaim = httpContextAccessor.HttpContext?
        .User?.FindFirst(System.Security.Claims.ClaimTypes
            .NameIdentifier)?.Value;
    
    if (string.IsNullOrEmpty(userIdClaim))
    {
        return new PlaceOrderResult(
            Success: false,
            ErrorMessage: "User not authenticated.",
            OrderId: null);
    }

    var userId = Guid.Parse(userIdClaim);
    if (input.Items is null || !input.Items.Any())
    {
        return new PlaceOrderResult(
            Success: false,
            ErrorMessage: "Order must have at least one item.",
            OrderId: null);
    }

    decimal totalAmount = 0;
    var orderItems = new List<OrderItem>();
    var reservedItems = new List<(Guid ProductId, int Quantity)>();

    // Generate order ID upfront
    // We need it for the gRPC reservation call
    var orderId = Guid.NewGuid();

    foreach (var item in input.Items)
    {
        // Verify product exists in database
        var product = await db.Products
            .FirstOrDefaultAsync(p =>
                p.Id == item.ProductId &&
                p.TenantId == tenantContext.TenantId &&
                p.IsActive);

        if (product is null)
        {
            // Release any already reserved stock
            // before returning error
            await ReleaseReservedStock(
                reservedItems,
                tenantContext.TenantId,
                orderId,
                warehouseService);

            return new PlaceOrderResult(
                Success: false,
                ErrorMessage:
                    $"Product {item.ProductId} not found.",
                OrderId: null);
        }

        // ── GRPC CALL TO WAREHOUSE ─────────────────────────
        // This is the key new step
        // Call Warehouse service to reserve stock
        var reservation = await warehouseService
            .ReserveStockAsync(
                productId: product.Id,
                tenantId: tenantContext.TenantId,
                quantity: item.Quantity,
                orderId: orderId);

        if (!reservation.Success)
        {
            // Stock not available
            // Release any stock we already reserved
            await ReleaseReservedStock(
                reservedItems,
                tenantContext.TenantId,
                orderId,
                warehouseService);

            return new PlaceOrderResult(
                Success: false,
                ErrorMessage: reservation.Message,
                OrderId: null);
        }

        // Track what we reserved so we can release
        // if a later item fails
        reservedItems.Add((product.Id, item.Quantity));

        var orderItem = new OrderItem
        {
            ProductId = product.Id,
            Quantity = item.Quantity,
            UnitPrice = product.Price
        };

        orderItems.Add(orderItem);
        totalAmount += product.Price * item.Quantity;
    }

    // All items reserved successfully
    // Now save the order to database
    var order = new Order
    {
        Id = orderId, // use the same ID we used for reservations
        TenantId = tenantContext.TenantId,
        UserId = userId,
        Status = OrderStatus.Confirmed, // Confirmed because stock is reserved
        TotalAmount = totalAmount,
        Notes = input.Notes,
        Items = orderItems
    };

    db.Orders.Add(order);
    await db.SaveChangesAsync();

    return new PlaceOrderResult(
        Success: true,
        ErrorMessage: null,
        OrderId: order.Id);
}

// Helper method to release all reserved stock on failure
// Important for data consistency
// If we reserved stock for items 1 and 2
// but item 3 fails — we must release items 1 and 2
private async Task ReleaseReservedStock(
    List<(Guid ProductId, int Quantity)> reservedItems,
    Guid tenantId,
    Guid orderId,
    IWarehouseService warehouseService)
{
    foreach (var (productId, quantity) in reservedItems)
    {
        await warehouseService.ReleaseStockAsync(
            productId, tenantId, quantity, orderId);
    }
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
    
    // Add this mutation to get a presigned URL
// React calls this first, then uploads directly to S3
    public async Task<UploadUrlResult> GetProductImageUploadUrl(
        GetUploadUrlInput input,
        ITenantContext tenantContext,
        IS3Service s3Service)
    {
        var result = await s3Service
            .GenerateProductImageUploadUrlAsync(
                productId: input.ProductId,
                tenantId: tenantContext.TenantId,
                fileExtension: input.FileExtension);

        return new UploadUrlResult(
            UploadUrl: result.UploadUrl,
            S3Key: result.S3Key,
            ExpiresAt: result.ExpiresAt.ToString("o"));
    }
}