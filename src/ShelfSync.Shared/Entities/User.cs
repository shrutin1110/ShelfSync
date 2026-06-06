namespace ShelfSync.Shared.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // THIS is the multi-tenancy key
    // Every user belongs to exactly one tenant
    // When you query users, you always filter by this
    public Guid TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    // NEVER store plain text passwords
    // PasswordHash stores a BCrypt hash like "$2a$11$..."
    // BCrypt is a one-way function — you can't reverse it to get the password
    public string PasswordHash { get; set; } = string.Empty;

    // "admin", "seller", or "viewer"
    public string Role { get; set; } = "seller";

    // Refresh token is stored here after login
    // null means user hasn't logged in yet or logged out
    public string? RefreshToken { get; set; }
    
    public DateTime? RefreshTokenExpiry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Tenant Tenant { get; set; } = null!;
}