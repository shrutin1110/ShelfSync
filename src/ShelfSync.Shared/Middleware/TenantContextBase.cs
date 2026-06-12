using Microsoft.AspNetCore.Http;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Shared.Middleware;

public class TenantContextBase : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextBase(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Instead of being set by middleware
    // these properties read DIRECTLY from HttpContext every time
    // This means they are always fresh and always correct
    public Guid TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User?.FindFirst("tenantId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public string TenantName =>
        _httpContextAccessor.HttpContext?
            .User?.FindFirst("tenantName")?.Value ?? string.Empty;

    public string Plan =>
        _httpContextAccessor.HttpContext?
            .User?.FindFirst("plan")?.Value ?? string.Empty;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?
            .User?.Identity?.IsAuthenticated ?? false;
}