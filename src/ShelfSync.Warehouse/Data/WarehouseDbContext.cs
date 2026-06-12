using Microsoft.EntityFrameworkCore;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Warehouse.Data;

// Warehouse service only needs these two tables
// WarehouseLocations = where products are stored
// Products = to verify product exists and update stock
public class WarehouseDbContext : DbContext
{
    public WarehouseDbContext(
        DbContextOptions<WarehouseDbContext> options)
        : base(options) { }

    public DbSet<WarehouseLocation> WarehouseLocations
        => Set<WarehouseLocation>();

    public DbSet<Product> Products
        => Set<Product>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WarehouseLocation>(entity =>
        {
            entity.HasKey(w => w.Id);

            entity.HasOne(w => w.Product)
                .WithMany()
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Price)
                .HasPrecision(18, 2);
        });
    }
}