using System.Text.Json;
using StackExchange.Redis;

namespace ShelfSync.Orders.Cache;

public class CacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly IServer _server;
    private readonly ILogger<CacheService> _logger;

    // IConnectionMultiplexer is the Redis connection
    // It manages a pool of connections to Redis
    // Singleton lifetime — expensive to create
    // One multiplexer shared across all requests
    public CacheService(
        IConnectionMultiplexer redis,
        ILogger<CacheService> logger)
    {
        // GetDatabase() returns a lightweight proxy
        // to the Redis database
        // This is cheap to call — no new connection
        _db = redis.GetDatabase();

        // IServer is used for advanced operations
        // like pattern-based key deletion
        _server = redis.GetServer(
            redis.GetEndPoints().First());

        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            // Try to get the value from Redis
            var value = await _db.StringGetAsync(key);

            // RedisValue.IsNullOrEmpty means key does not exist
            // or has expired (Redis auto-deletes expired keys)
            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug(
                    "Cache MISS for key: {Key}", key);
                return default; // null for reference types
            }

            _logger.LogDebug(
                "Cache HIT for key: {Key}", key);

            // Deserialize the JSON back to the C# type
            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (Exception ex)
        {
            // If Redis is down → log and return null
            // App continues without cache (degrades gracefully)
            // This is important — cache failure should not
            // crash your application
            _logger.LogError(ex,
                "Redis GET failed for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key, T value, TimeSpan? ttl = null)
    {
        try
        {
            // Serialize the C# object to JSON for storage
            var serialized = JsonSerializer.Serialize(value);

            // StringSetAsync stores the value in Redis
            // with optional TTL (expiry time)
            // StackExchange.Redis uses its own Expiration type
// Convert TimeSpan? to TimeSpan with a fallback
            if (ttl.HasValue)
            {
                await _db.StringSetAsync(key, serialized, ttl.Value);
            }
            else
            {
                await _db.StringSetAsync(key, serialized);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Redis SET failed for key: {Key}", key);
            // Do not throw — cache failure is not fatal
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
            _logger.LogDebug(
                "Cache REMOVE for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Redis DELETE failed for key: {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            // KEYS command finds all keys matching a pattern
            // e.g. "products:*" finds all product cache keys
            //
            // WARNING: KEYS is slow on large Redis databases
            // In production use SCAN instead
            // For this project KEYS is fine
            var keys = _server
                .Keys(pattern: pattern)
                .ToArray();

            if (keys.Any())
            {
                await _db.KeyDeleteAsync(keys);
                _logger.LogDebug(
                    "Cache REMOVE pattern: {Pattern}, " +
                    "deleted {Count} keys",
                    pattern, keys.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Redis pattern DELETE failed: {Pattern}",
                pattern);
        }
    }
}