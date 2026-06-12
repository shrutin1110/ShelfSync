using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.GraphQL.Types;

// OrderType tells Hot Chocolate:
// "when someone queries an Order, these fields are available"
// You can expose all entity fields or just some
// This is how you control what clients can see
public class OrderType : ObjectType<Order>
{
    protected override void Configure(
        IObjectTypeDescriptor<Order> descriptor)
    {
        // Give this type a name in the GraphQL schema
        descriptor.Name("Order");

        // Expose these fields to GraphQL clients
        descriptor.Field(o => o.Id);
        descriptor.Field(o => o.Status);
        descriptor.Field(o => o.TotalAmount);
        descriptor.Field(o => o.Notes);
        descriptor.Field(o => o.CreatedAt);
        descriptor.Field(o => o.UpdatedAt);

        // Items is a navigation property
        // UseProjection tells EF Core to only load
        // the fields the client actually asked for
        descriptor.Field(o => o.Items)
                  .UseProjection();

        // Do NOT expose TenantId — clients don't need it
        // and it could expose internal implementation details
        descriptor.Ignore(o => o.TenantId);
        descriptor.Ignore(o => o.UserId);
    }
}