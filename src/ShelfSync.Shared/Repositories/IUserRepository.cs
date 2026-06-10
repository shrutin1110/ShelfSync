using ShelfSync.Shared.Entities;

namespace ShelfSync.Shared.Repositories;

// IUserRepository extends the base with User-specific queries
// All the basic CRUD comes from IBaseRepository<User>
// These are extra methods only Users need
public interface IUserRepository : IBaseRepository<User>
{
    // Find a user by email within the current tenant
    // Used during login to look up the user
    Task<User?> GetByEmailAsync(string email);

    // Find a user by their refresh token
    // Used during token refresh to validate the token
    Task<User?> GetByRefreshTokenAsync(string refreshToken);
}