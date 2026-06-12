using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.GraphQL.Types;

public class OrderItemType : ObjectType<OrderItem>
{
    protected override void Configure(
        IObjectTypeDescriptor<OrderItem> descriptor)
    {
        descriptor.Name("OrderItem");

        descriptor.Field(oi => oi.Id);
        descriptor.Field(oi => oi.Quantity);
        descriptor.Field(oi => oi.UnitPrice);

        // Include the product details
        // so client can get product name in same query
        descriptor.Field(oi => oi.Product)
            .UseProjection();

        descriptor.Ignore(oi => oi.OrderId);
        descriptor.Ignore(oi => oi.ProductId);
    }
}