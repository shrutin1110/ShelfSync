namespace ShelfSync.Shared.Repositories;

// Generic interface for basic database operations
// T is a placeholder for any entity type
// e.g. IBaseRepository<Order> works with Orders
//      IBaseRepository<Product> works with Products
//
// This defines the CONTRACT — what every repository can do
public interface IBaseRepository<T> where T : class
{
    // Get all records belonging to the current tenant
    // TenantId filtering happens inside — controller doesn't know
    Task<List<T>> GetAllAsync();

    // Get one record by its ID
    // Returns null if not found OR if it belongs to different tenant
    Task<T?> GetByIdAsync(Guid id);

    // Save a new record — TenantId is stamped automatically
    Task<T> CreateAsync(T entity);

    // Update an existing record
    Task<T> UpdateAsync(T entity);

    // Delete a record by ID
    Task<bool> DeleteAsync(Guid id);
}