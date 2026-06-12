using ShelfSync.Shared.Middleware;
using Microsoft.AspNetCore.Http;

namespace ShelfSync.Auth.Middleware;

public class TenantContext : TenantContextBase
{
    public TenantContext(IHttpContextAccessor httpContextAccessor)
        : base(httpContextAccessor) { }
}