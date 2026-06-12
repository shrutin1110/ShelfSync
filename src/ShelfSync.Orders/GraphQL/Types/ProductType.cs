using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.GraphQL.Types;

public class ProductType : ObjectType<Product>
{
    protected override void Configure(
        IObjectTypeDescriptor<Product> descriptor)
    {
        descriptor.Name("Product");

        descriptor.Field(p => p.Id);
        descriptor.Field(p => p.Name);
        descriptor.Field(p => p.SKU);
        descriptor.Field(p => p.Price);
        descriptor.Field(p => p.StockQuantity);
        descriptor.Field(p => p.IsActive);
        descriptor.Field(p => p.CreatedAt);

        // Hide internal fields
        descriptor.Ignore(p => p.TenantId);
        descriptor.Ignore(p => p.S3ImageKey);
        descriptor.Ignore(p => p.OrderItems);
    }
}