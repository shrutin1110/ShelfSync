using GreenDonut;
using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Data;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.DataLoaders;

// ProductDataLoader batches multiple product lookups
// into a single database query
//
// Instead of:
//   SELECT * FROM Products WHERE Id = 'aaa'
//   SELECT * FROM Products WHERE Id = 'bbb'
//   SELECT * FROM Products WHERE Id = 'ccc'
//
// It does:
//   SELECT * FROM Products WHERE Id IN ('aaa', 'bbb', 'ccc')
//
// BatchDataLoader<TKey, TValue>:
//   TKey   = the type of ID you look up by (Guid)
//   TValue = the type of object you get back (Product)
public class ProductDataLoader
    : BatchDataLoader<Guid, Product>
{
    private readonly IDbContextFactory<OrdersDbContext> _dbFactory;

    // IMPORTANT: DataLoader does NOT use regular DbContext injection
    // It uses IDbContextFactory instead
    //
    // Why? DataLoader batches requests across multiple resolver calls
    // These resolvers might run in parallel on different threads
    // A single DbContext is NOT thread-safe
    // IDbContextFactory creates a fresh DbContext for each batch
    // which IS thread-safe
    public ProductDataLoader(
        IDbContextFactory<OrdersDbContext> dbFactory,
        IBatchScheduler batchScheduler,
        DataLoaderOptions? options = null)
        : base(batchScheduler, options)
    {
        _dbFactory = dbFactory;
    }

    // LoadBatchAsync is called once per batch
    // keys = all the product IDs collected from all resolvers
    // e.g. keys = [aaa, bbb, ccc, ddd, eee, ...]
    protected override async Task<IReadOnlyDictionary<Guid, Product>>
        LoadBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
    {
        // Create a fresh DbContext for this batch
        await using var db = await _dbFactory
            .CreateDbContextAsync(cancellationToken);

        // ONE query for ALL product IDs
        // keys.ToList() converts the IReadOnlyList to List
        // which EF Core can use in the SQL IN clause
        var products = await db.Products
            .Where(p => keys.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Return as dictionary: ProductId → Product
        // DataLoader uses this to distribute results
        // back to the correct resolvers
        return products.ToDictionary(p => p.Id);
    }
}