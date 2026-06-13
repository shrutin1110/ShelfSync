using ShelfSync.Orders.Services;
using ShelfSync.Shared.Entities;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Orders.GraphQL.Queries;

[QueryType]
public class ProductQuery
{
    // Now uses ProductService instead of hitting DB directly
    // ProductService handles cache-aside automatically
    public async Task<List<Product>> GetProducts(
        ITenantContext tenantContext,
        IProductService productService)
    {
        return await productService
            .GetProductsAsync(tenantContext.TenantId);
    }

    public async Task<Product?> GetProductById(
        Guid id,
        ITenantContext tenantContext,
        IProductService productService)
    {
        return await productService
            .GetProductByIdAsync(id, tenantContext.TenantId);
    }
}