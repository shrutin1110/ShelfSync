namespace ShelfSync.Orders.Cache;

// Centralise all cache key patterns here
// Prevents typos and makes refactoring easier
// If you change a key format → change it in one place
public static class CacheKeys
{
    // Product catalog per tenant
    // Format: products:{tenantId}
    // e.g.  : products:c075f7b5-9043-4823-ad17-66b003675586
    public static string ProductCatalog(Guid tenantId)
        => $"products:{tenantId}";

    // All product cache keys for a tenant (for pattern deletion)
    // Used when any product changes → invalidate ALL product cache
    public static string ProductCatalogPattern(Guid tenantId)
        => $"products:{tenantId}*";

    // Single product
    // Format: product:{productId}
    public static string Product(Guid productId)
        => $"product:{productId}";
}