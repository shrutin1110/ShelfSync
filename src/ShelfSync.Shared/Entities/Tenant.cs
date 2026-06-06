namespace ShelfSync.Shared.Entities;

public class Tenant
{
    // Guid.NewGuid() generates a new unique ID automatically
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    // Which pricing plan this tenant is on
    public string Plan { get; set; } = "free";

    public bool IsActive { get; set; } = true;

    // Always store dates in UTC — never local time
    // UTC avoids timezone bugs when your server and users are in different countries
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties — these are not columns in the database
    // They let EF Core load related data (e.g. tenant.Users gives you all users)
    // ICollection means "a list of" — one tenant has many users
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}