using Microsoft.EntityFrameworkCore;
using ShelfSync.Auth.Data;
using ShelfSync.Shared.Entities;
using ShelfSync.Shared.Interfaces;
using ShelfSync.Shared.Repositories;

namespace ShelfSync.Auth.Repositories;

// UserRepository inherits ALL generic CRUD from BaseRepository<User>
// and adds User-specific queries
public class UserRepository : BaseRepository<User>, IUserRepository
{
    // Call the base constructor to set up _db, _tenant, _dbSet
    public UserRepository(AppDbContext db, ITenantContext tenant)
        : base(db, tenant) { }

    public async Task<User?> GetByEmailAsync(string email)
    {
        // Note: email lookup does NOT filter by TenantId
        // Why? Because on login you only know the email
        // not which tenant they belong to yet
        // The tenant is determined AFTER finding the user
        return await _dbSet
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
    {
        return await _dbSet
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
    }
}