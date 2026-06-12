using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Data;
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
}