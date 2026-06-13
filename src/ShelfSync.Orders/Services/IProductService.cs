using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.Services;

public interface IProductService
{
    // Get all active products for a tenant
    // Returns from cache if available
    // Falls back to database if cache miss
    Task<List<Product>> GetProductsAsync(Guid tenantId);

    // Get one product by ID
    Task<Product?> GetProductByIdAsync(
        Guid productId, Guid tenantId);

    // Invalidate cache when products change
    // Called after add, update, or delete
    Task InvalidateProductCacheAsync(Guid tenantId);
}