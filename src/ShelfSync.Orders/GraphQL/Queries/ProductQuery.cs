using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Data;
using ShelfSync.Shared.Entities;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Orders.GraphQL.Queries;

[QueryType]
public class ProductQuery
{
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Product> GetProducts(
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        return db.Products
            .Where(p =>
                p.TenantId == tenantContext.TenantId &&
                p.IsActive);
    }

    public async Task<Product?> GetProductById(
        Guid id,
        OrdersDbContext db,
        ITenantContext tenantContext)
    {
        return await db.Products
            .FirstOrDefaultAsync(p =>
                p.Id == id &&
                p.TenantId == tenantContext.TenantId &&
                p.IsActive);
    }
    
    public string GetDebugTenant(ITenantContext tenantContext)
    {
        return $"TenantId: {tenantContext.TenantId} | " +
               $"IsAuthenticated: {tenantContext.IsAuthenticated}";
    }
}