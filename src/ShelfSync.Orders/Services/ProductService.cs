using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Cache;
using ShelfSync.Orders.Data;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.Services;

public class ProductService : IProductService
{
    private readonly OrdersDbContext _db;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        OrdersDbContext db,
        ICacheService cache,
        IConfiguration configuration,
        ILogger<ProductService> logger)
    {
        _db = db;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<Product>> GetProductsAsync(
        Guid tenantId)
    {
        // Build the cache key for this tenant's products
        var cacheKey = CacheKeys.ProductCatalog(tenantId);

        // ── STEP 1: CHECK CACHE ───────────────────────────
        // This is the cache-aside pattern:
        // 1. Check cache first
        // 2. If found (cache hit) → return immediately
        // 3. If not found (cache miss) → go to database
        var cached = await _cache
            .GetAsync<List<Product>>(cacheKey);

        if (cached is not null)
        {
            _logger.LogInformation(
                "Products served from CACHE for tenant {TenantId}",
                tenantId);
            return cached;
        }

        // ── STEP 2: CACHE MISS → QUERY DATABASE ──────────
        _logger.LogInformation(
            "Cache MISS — querying DATABASE for tenant {TenantId}",
            tenantId);

        var products = await _db.Products
            .Where(p =>
                p.TenantId == tenantId &&
                p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        // ── STEP 3: STORE IN CACHE ────────────────────────
        // Read TTL from configuration
        // Default to 5 minutes if not configured
        var ttlMinutes = _configuration
            .GetValue<int>("Cache:ProductCatalogTtlMinutes", 5);

        await _cache.SetAsync(
            cacheKey,
            products,
            TimeSpan.FromMinutes(ttlMinutes));

        _logger.LogInformation(
            "Products stored in CACHE for tenant {TenantId}. " +
            "TTL: {TTL} minutes. Count: {Count}",
            tenantId, ttlMinutes, products.Count);

        return products;
    }

    public async Task<Product?> GetProductByIdAsync(
        Guid productId, Guid tenantId)
    {
        var cacheKey = CacheKeys.Product(productId);

        var cached = await _cache
            .GetAsync<Product>(cacheKey);

        if (cached is not null)
            return cached;

        var product = await _db.Products
            .FirstOrDefaultAsync(p =>
                p.Id == productId &&
                p.TenantId == tenantId &&
                p.IsActive);

        if (product is not null)
        {
            await _cache.SetAsync(
                cacheKey,
                product,
                TimeSpan.FromMinutes(10));
        }

        return product;
    }

    public async Task InvalidateProductCacheAsync(Guid tenantId)
    {
        // When a product is added, updated, or deleted
        // the cache is stale — delete it
        // Next request will rebuild from database
        //
        // This is called "cache invalidation"
        // One of the hardest problems in computer science
        // (along with naming things and off-by-one errors)
        var pattern = CacheKeys.ProductCatalogPattern(tenantId);
        await _cache.RemoveByPatternAsync(pattern);

        _logger.LogInformation(
            "Product cache INVALIDATED for tenant {TenantId}",
            tenantId);
    }
}