using Microsoft.EntityFrameworkCore;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Warehouse.Data;

public static class SeedData
{
    // Call this on startup to add test warehouse locations
    // Only seeds if no locations exist yet
    public static async Task SeedAsync(WarehouseDbContext db)
    {
        // Check if already seeded
        if (await db.WarehouseLocations.AnyAsync())
            return;

        // Get all products from the database
        var products = await db.Products.ToListAsync();

        if (!products.Any())
        {
            Console.WriteLine(
                "No products found. " +
                "Add products via Orders service first.");
            return;
        }

        var locations = new List<WarehouseLocation>();
        var aisles = new[] { "A", "B", "C", "D" };
        var shelfNum = 1;

        foreach (var product in products)
        {
            locations.Add(new WarehouseLocation
            {
                TenantId = product.TenantId,
                ProductId = product.Id,
                Aisle = aisles[shelfNum % 4],
                Shelf = shelfNum.ToString(),
                QuantityAvailable = 100 // start with 100 units each
            });
            shelfNum++;
        }

        db.WarehouseLocations.AddRange(locations);
        await db.SaveChangesAsync();

        Console.WriteLine(
            $"Seeded {locations.Count} warehouse locations.");
    }
}