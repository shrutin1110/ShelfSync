using System.Security.Claims;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Auth.Middleware;

// Middleware must follow this exact pattern:
// - Constructor receives RequestDelegate (the next middleware)
// - Has an InvokeAsync method that receives HttpContext
// .NET calls InvokeAsync for every HTTP request
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    // RequestDelegate represents the next piece of middleware
    // or the controller if this is the last middleware
    // You call _next(context) to pass the request along
    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // HttpContext contains everything about the current request:
    //   - Headers
    //   - User (claims from JWT)
    //   - Request body
    //   - Response
    //
    // ITenantContext is injected here — NOT in the constructor
    // Why? Because ITenantContext is Scoped (new per request)
    // Middleware is Singleton (one instance for app lifetime)
    // You cannot inject Scoped services into Singleton constructors
    // Instead you receive them as method parameters here
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext)
    {
        // context.User.Identity?.IsAuthenticated
        // checks if the JWT was valid and user is logged in
        // If no JWT was sent, or JWT was invalid → false
        // If valid JWT → true
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Cast ITenantContext to TenantContext so we can SET values
            // ITenantContext only has getters (read-only)
            // TenantContext has setters (read + write)
            // The cast is safe because we registered TenantContext
            // as the implementation of ITenantContext
            var ctx = (TenantContext)tenantContext;

            // FindFirstValue searches the JWT claims for the given type
            // and returns the value as a string
            // The ?? operator means "if null, use empty string instead"
            var tenantIdClaim = context.User.FindFirstValue("tenantId");
            var tenantNameClaim = context.User.FindFirstValue("tenantName");
            var planClaim = context.User.FindFirstValue("plan");

            // Only set tenant context if tenantId claim exists
            // (public endpoints won't have it)
            if (!string.IsNullOrEmpty(tenantIdClaim))
            {
                // Guid.Parse converts the string "3f2504e0-..."
                // back to a Guid object
                ctx.TenantId = Guid.Parse(tenantIdClaim);
                ctx.TenantName = tenantNameClaim ?? string.Empty;
                ctx.Plan = planClaim ?? string.Empty;
                ctx.IsAuthenticated = true;
            }
        }

        // Pass the request to the next middleware
        // If you forget this line — the request stops here
        // and no response is ever sent. Always call _next!
        await _next(context);
    }
}