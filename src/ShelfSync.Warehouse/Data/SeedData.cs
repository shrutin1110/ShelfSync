using Microsoft.EntityFrameworkCore;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Warehouse.Data;

public static class SeedData
{
    public static async Task SeedAsync(WarehouseDbContext db)
    {
        try
        {
            // Run migrations first
            await db.Database.MigrateAsync();

            if (await db.WarehouseLocations.AnyAsync())
                return;

            var products = await db.Products.ToListAsync();

            if (!products.Any())
            {
                Console.WriteLine("No products found to seed.");
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
                    QuantityAvailable = 100
                });
                shelfNum++;
            }

            db.WarehouseLocations.AddRange(locations);
            await db.SaveChangesAsync();

            Console.WriteLine(
                $"Seeded {locations.Count} warehouse locations.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Seed error: {ex.Message}");
        }
    }
}