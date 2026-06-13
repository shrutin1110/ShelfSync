namespace ShelfSync.Orders.Cache;

// Generic cache interface
// T can be any type — Product list, Order, etc.
public interface ICacheService
{
    // Try to get a value from cache
    // Returns null if not found or expired
    Task<T?> GetAsync<T>(string key);

    // Store a value in cache with a TTL
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null);

    // Remove a specific key from cache
    // Used for cache invalidation
    Task RemoveAsync(string key);

    // Remove all keys matching a pattern
    // e.g. remove all "products:*" keys
    Task RemoveByPatternAsync(string pattern);
}