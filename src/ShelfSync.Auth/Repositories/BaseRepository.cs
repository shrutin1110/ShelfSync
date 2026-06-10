using Microsoft.EntityFrameworkCore;
using ShelfSync.Auth.Data;
using ShelfSync.Shared.Interfaces;
using ShelfSync.Shared.Repositories;

namespace ShelfSync.Auth.Repositories;

// BaseRepository<T> implements IBaseRepository<T>
// T is constrained to be a class with an Id (Guid) and TenantId (Guid)
// Every entity in your system has these two properties
//
// "where T : class" = T must be a reference type (not int, bool etc.)
public class BaseRepository<T> : IBaseRepository<T>
    where T : class
{
    protected readonly AppDbContext _db;
    protected readonly ITenantContext _tenant;

    // DbSet<T> is the table for entity T
    // e.g. if T = Order, _dbSet = _db.Orders
    // EF Core figures this out automatically from AppDbContext
    protected readonly DbSet<T> _dbSet;

    public BaseRepository(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;

        // Set<T>() returns the DbSet for entity T from the DbContext
        // This is how we make the repository generic
        // without hardcoding which table to use
        _dbSet = db.Set<T>();
    }

    public async Task<List<T>> GetAllAsync()
    {
        // EF Core's AsQueryable() lets us build a query step by step
        // We add .Where() for tenant filtering
        //
        // EF.Property<Guid>(entity, "TenantId") is how you access
        // a property by name when using generics
        // Because T is generic, we cannot write entity.TenantId directly
        // EF.Property is the EF Core way to access properties by name
        return await _dbSet
            .Where(entity =>
                EF.Property<Guid>(entity, "TenantId") == _tenant.TenantId)
            .ToListAsync();
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(entity =>
                EF.Property<Guid>(entity, "Id") == id &&
                EF.Property<Guid>(entity, "TenantId") == _tenant.TenantId)
            .FirstOrDefaultAsync();

        // Note: we check BOTH Id AND TenantId
        // Why? Imagine Order ID "ggg-777" belongs to Acme Clothing
        // A malicious Beta Electronics user sends request for "ggg-777"
        // Without TenantId check → they get Acme's order
        // With TenantId check → returns null → 404 Not Found
        // This is called "Insecure Direct Object Reference" prevention
        // A real security vulnerability if you forget the tenant check
    }

    public async Task<T> CreateAsync(T entity)
    {
        // Add entity to the DbSet (tracked by EF Core)
        _dbSet.Add(entity);

        // SaveChangesAsync sends INSERT to the database
        await _db.SaveChangesAsync();

        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        // Update tells EF Core this entity has changed
        // EF Core generates an UPDATE SQL statement
        _db.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        // First find the entity — with tenant check for security
        var entity = await GetByIdAsync(id);

        if (entity is null)
            return false; // not found or belongs to different tenant

        _dbSet.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}