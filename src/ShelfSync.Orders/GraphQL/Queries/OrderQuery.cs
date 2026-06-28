using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Data;
using ShelfSync.Shared.Entities;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Orders.GraphQL.Queries;

[QueryType]
public class OrderQuery
{
    // Using Include to load items and products
    // More straightforward than DataLoader for this use case
    // DataLoader optimisation can be added later
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Order> GetOrders(
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        return db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.TenantId == tenantContext.TenantId);
    }

    public async Task<Order?> GetOrderById(
        Guid id,
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        return await db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o =>
                o.Id == id &&
                o.TenantId == tenantContext.TenantId);
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