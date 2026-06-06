using Microsoft.EntityFrameworkCore;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Auth.Data;

public class AppDbContext : DbContext
{
    // This constructor receives the database connection settings
    // injected by the dependency injection system
    // You never call this yourself — .NET calls it automatically
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    // DbSet = a table in the database
    // DbSet<Tenant> = the Tenants table
    // => Set<Tenant>() is shorthand that works better with nullability checks
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    // OnModelCreating is where you configure the fine details of each table
    // Things like: which column is unique, what's the max length, how tables relate
    // EF Core reads this when creating migrations
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── TENANT ────────────────────────────────────────────

        modelBuilder.Entity<Tenant>(entity =>
        {
            // Primary key — the unique identifier for each row
            entity.HasKey(t => t.Id);

            // Name is required (NOT NULL in SQL) and max 200 characters
            entity.Property(t => t.Name)
                  .IsRequired()
                  .HasMaxLength(200);

            // If no plan is provided, default to "free"
            entity.Property(t => t.Plan)
                  .HasDefaultValue("free");
        });

        // ── USER ──────────────────────────────────────────────

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            // IsUnique() creates a UNIQUE INDEX on Email
            // This means the database itself prevents duplicate emails
            // Not just your application code — the DB enforces it too
            entity.HasIndex(u => u.Email).IsUnique();

            entity.Property(u => u.Email)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.Property(u => u.Role)
                  .HasDefaultValue("seller");

            // Define the relationship: User belongs to one Tenant
            // HasOne → WithMany means: one Tenant has many Users
            // HasForeignKey tells EF which column is the foreign key
            // OnDelete Cascade means: if you delete a Tenant,
            // all their Users get deleted automatically
            entity.HasOne(u => u.Tenant)
                  .WithMany(t => t.Users)
                  .HasForeignKey(u => u.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PRODUCT ───────────────────────────────────────────

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Name)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.Property(p => p.SKU)
                  .IsRequired()
                  .HasMaxLength(100);

            // HasPrecision(18, 2) means: up to 18 digits total, 2 after the decimal
            // e.g. 9999999999999999.99 is valid
            // This is the standard for storing money in PostgreSQL
            entity.Property(p => p.Price)
                  .HasPrecision(18, 2);

            entity.HasOne(p => p.Tenant)
                  .WithMany(t => t.Products)
                  .HasForeignKey(p => p.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ORDER ─────────────────────────────────────────────

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.TotalAmount)
                  .HasPrecision(18, 2);

            // HasConversion<string>() stores the enum as text in the database
            // Without this, EF stores OrderStatus.Pending as 0, Confirmed as 1 etc.
            // Storing as "Pending" is much more readable when you inspect the DB directly
            entity.Property(o => o.Status)
                  .HasConversion<string>();

            entity.HasOne(o => o.Tenant)
                  .WithMany(t => t.Orders)
                  .HasForeignKey(o => o.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict means: don't delete the User if they have orders
            // You want to keep order history even if a user account is removed
            entity.HasOne(o => o.User)
                  .WithMany()
                  .HasForeignKey(o => o.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ORDER ITEM ────────────────────────────────────────

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.Id);

            entity.Property(oi => oi.UnitPrice)
                  .HasPrecision(18, 2);

            // Cascade: delete order → delete all its items
            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict: don't delete a product if it's in an order
            entity.HasOne(oi => oi.Product)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(oi => oi.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── WAREHOUSE LOCATION ────────────────────────────────

        modelBuilder.Entity<WarehouseLocation>(entity =>
        {
            entity.HasKey(w => w.Id);

            entity.HasOne(w => w.Tenant)
                  .WithMany()
                  .HasForeignKey(w => w.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(w => w.Product)
                  .WithMany()
                  .HasForeignKey(w => w.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── INVOICE ───────────────────────────────────────────

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(i => i.Id);

            // HasOne → WithOne means ONE-to-ONE relationship
            // One Order has exactly one Invoice
            // HasForeignKey<Invoice> means the Invoice table holds the foreign key
            entity.HasOne(i => i.Order)
                  .WithOne(o => o.Invoice)
                  .HasForeignKey<Invoice>(i => i.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}