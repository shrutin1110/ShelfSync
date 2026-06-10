using Microsoft.EntityFrameworkCore;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Orders.Data;

// Orders service only needs these 4 tables
// It does NOT need WarehouseLocations or Invoices
// Each service only has access to what it needs
// This is the principle of least privilege applied to data
public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.TotalAmount)
                  .HasPrecision(18, 2);
            entity.Property(o => o.Status)
                  .HasConversion<string>();

            entity.HasOne(o => o.Tenant)
                  .WithMany(t => t.Orders)
                  .HasForeignKey(o => o.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // OrderItem configuration
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.Id);
            entity.Property(oi => oi.UnitPrice)
                  .HasPrecision(18, 2);

            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.Product)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(oi => oi.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Price)
                  .HasPrecision(18, 2);

            entity.HasOne(p => p.Tenant)
                  .WithMany(t => t.Products)
                  .HasForeignKey(p => p.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}