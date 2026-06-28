namespace ShelfSync.Shared.Interfaces;

// This interface defines what information about the current tenant
// is available throughout the application
//
// Any class can ask for ITenantContext and get
// the current request's tenant information
// without touching the JWT or the database
public interface ITenantContext
{
    // The unique ID of the tenant making this request
    // Every database query uses this to filter data
    Guid TenantId { get; }

    // The company name — useful for logging and display
    string TenantName { get; }

    // Which pricing plan — used to enable/disable features
    // e.g. "only Pro tenants can export CSV"
    string Plan { get; }

    // Whether the current request is from an authenticated tenant
    // Some endpoints are public (no token) — IsAuthenticated is false there
    bool IsAuthenticated { get; }
    
    string Role { get; }
    Guid UserId { get; }
}
