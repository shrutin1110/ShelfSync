namespace ShelfSync.Shared.Entities;

// Tracks WHERE in the physical warehouse a product is stored
// e.g. Aisle B, Shelf 3 has 50 units of product X
public class WarehouseLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Multi-tenancy
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }

    // Physical location in the warehouse
    public string Aisle { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;

    // How many units are physically at this location
    public int QuantityAvailable { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Product Product { get; set; } = null!;
}