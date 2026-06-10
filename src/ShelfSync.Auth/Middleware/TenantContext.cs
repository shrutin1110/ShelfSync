using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Auth.Middleware;

// TenantContext is the concrete implementation of ITenantContext
// It has internal setters so ONLY the middleware can set values
// Other classes can only READ through the ITenantContext interface
//
// Why internal setters?
// Security — you don't want random code changing the TenantId mid-request
// Only TenantMiddleware should set these values once per request
public class TenantContext : ITenantContext
{
    // The { get; set; } allows TenantMiddleware to set values
    // But since we register it as ITenantContext in DI,
    // other classes only see the read-only interface
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
}