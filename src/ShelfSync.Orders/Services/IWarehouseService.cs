namespace ShelfSync.Orders.Services;

// Interface defines what the warehouse service can do
// from the Orders service perspective
public interface IWarehouseService
{
    // Check and reserve stock before confirming order
    Task<StockReservationResult> ReserveStockAsync(
        Guid productId,
        Guid tenantId,
        int quantity,
        Guid orderId);

    // Release stock when order is cancelled
    Task<bool> ReleaseStockAsync(
        Guid productId,
        Guid tenantId,
        int quantity,
        Guid orderId);

    // Get current stock level
    Task<int> GetInventoryLevelAsync(
        Guid productId,
        Guid tenantId);
}

// Simple result object returned from ReserveStock
public record StockReservationResult(
    bool Success,
    string Message,
    int RemainingQuantity);