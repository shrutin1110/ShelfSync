using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Data;
using ShelfSync.Orders.DataLoaders;
using ShelfSync.Shared.Entities;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Orders.GraphQL.Queries;

// Query class contains all the READ operations
// Each public method becomes a queryable field in GraphQL
// Method name = field name (camelCase in GraphQL)
// e.g. GetOrders → orders in GraphQL
[QueryType]
public class OrderQuery
{
    
    /*
    // [UseProjection] → only load fields client asks for
    //   if client asks for just "id" and "status"
    //   EF only SELECT id, status — not all columns
    //
    // [UseFiltering] → client can filter results
    //   orders(where: { status: { eq: "Pending" } })
    //
    // [UseSorting] → client can sort results
    //   orders(order: { createdAt: DESC })
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Order> GetOrders(
        // OrdersDbContext injected from DI
        OrdersDbContext db,
        // ITenantContext gives us the current tenant
        ITenantContext tenantContext)
    {
        // IQueryable is important here — NOT ToList()
        // IQueryable means the query is not executed yet
        // Hot Chocolate adds the projection, filtering, sorting
        // THEN executes one optimized SQL query
        // If you use ToList() here EF loads ALL data first
        // then Hot Chocolate filters in memory — very inefficient
        return db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.TenantId == tenantContext.TenantId);
    }

    // Get a single order by ID
    public async Task<Order?> GetOrderById(
        Guid id,
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        // Check BOTH id AND tenantId
        // Prevents tenant A from reading tenant B's orders
        // by guessing IDs (IDOR prevention)
        return await db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o =>
                o.Id == id &&
                o.TenantId == tenantContext.TenantId);
    }
    
    */
    public IQueryable<Order> GetOrders(
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        // Notice: NO .Include() here anymore
        // Items are loaded by the resolver below using DataLoader
        return db.Orders
            .Where(o => o.TenantId == tenantContext.TenantId);
    }

    public async Task<Order?> GetOrderById(
        Guid id,
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        return await db.Orders
            .FirstOrDefaultAsync(o =>
                o.Id == id &&
                o.TenantId == tenantContext.TenantId);
    }

    // This resolver handles loading items for a specific order
    // It is called ONCE PER ORDER but DataLoader batches
    // all calls into one database query
    //
    // [Parent] = the Order object being resolved
    // Hot Chocolate calls this for each Order in the result set
    // DataLoader collects all OrderIds and batches them
    public async Task<IEnumerable<OrderItem>> GetItemsAsync(
        [Parent] Order order,
        OrderItemDataLoader orderItemDataLoader)
    {
        // This does NOT hit the database immediately
        // DataLoader collects this ID along with all other order IDs
        // Then fires ONE batch query at the end
        return await orderItemDataLoader.LoadAsync(order.Id);
    }

    // This resolver handles loading the product for a specific item
    // Called once per OrderItem but batched by DataLoader
    public async Task<Product?> GetProductAsync(
        [Parent] OrderItem item,
        ProductDataLoader productDataLoader)
    {
        // Same pattern — DataLoader collects all product IDs
        // fires ONE query for all of them
        return await productDataLoader.LoadAsync(item.ProductId);
    }

    public string GetDebugTenant(
        ITenantContext tenantContext,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var httpIsAuth = httpContext?.User?.Identity?
            .IsAuthenticated ?? false;
        var tenantClaim = httpContext?.User?
            .FindFirst("tenantId")?.Value;
        var authHeader = httpContext?.Request?
            .Headers["Authorization"].ToString() ?? "MISSING";
        var headerPreview = authHeader.Length > 30
            ? authHeader.Substring(0, 30) + "..."
            : authHeader;

        return
            $"TenantContext.IsAuthenticated: " +
            $"{tenantContext.IsAuthenticated}\n" +
            $"TenantContext.TenantId: {tenantContext.TenantId}\n" +
            $"HttpContext.User.IsAuthenticated: {httpIsAuth}\n" +
            $"TenantId claim: {tenantClaim ?? "NOT FOUND"}\n" +
            $"Auth header: {headerPreview}";
    }
}