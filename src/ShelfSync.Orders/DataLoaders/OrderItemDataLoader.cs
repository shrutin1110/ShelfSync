using GreenDonut;
using Microsoft.EntityFrameworkCore;
using ShelfSync.Orders.Data;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.DataLoaders;

// OrderItemDataLoader batches loading of order items
// Instead of one query per order, one query for all orders
//
// TKey   = OrderId (Guid)
// TValue = List<OrderItem> because one order has MANY items
public class OrderItemDataLoader
    : GroupedDataLoader<Guid, OrderItem>
{
    private readonly IDbContextFactory<OrdersDbContext> _dbFactory;

    // GroupedDataLoader vs BatchDataLoader:
    //
    // BatchDataLoader   → one key returns ONE value
    //                     e.g. ProductId → Product
    //
    // GroupedDataLoader → one key returns MANY values
    //                     e.g. OrderId → List<OrderItem>
    //                     "grouped" because items are grouped by key
    public OrderItemDataLoader(
        IDbContextFactory<OrdersDbContext> dbFactory,
        IBatchScheduler batchScheduler,
        DataLoaderOptions? options = null)
        : base(batchScheduler, options)
    {
        _dbFactory = dbFactory;
    }

    protected override async Task<ILookup<Guid, OrderItem>>
        LoadGroupedBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory
            .CreateDbContextAsync(cancellationToken);

        // Load ALL items for ALL order IDs in one query
        var items = await db.OrderItems
            .Where(oi => keys.Contains(oi.OrderId))
            .Include(oi => oi.Product)
            .ToListAsync(cancellationToken);

        // ToLookup groups items by OrderId
        // Like a dictionary but one key can have multiple values
        // OrderId aaa → [item1, item2]
        // OrderId bbb → [item3]
        // OrderId ccc → [item4, item5, item6]
        return items.ToLookup(oi => oi.OrderId);
    }
}