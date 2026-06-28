using Microsoft.AspNetCore.Mvc;
using ShelfSync.Auth.DTOs;
using ShelfSync.Auth.Services;
using ShelfSync.Auth.Data;
using ShelfSync.Shared.Entities;
using ShelfSync.Shared.Repositories;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShelfSync.Shared.Interfaces;

namespace ShelfSync.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IUserRepository _userRepository;

    public AuthController(
        AppDbContext db,
        ITokenService tokenService,
        IUserRepository userRepository)
    {
        _db = db;
        _tokenService = tokenService;
        _userRepository = userRepository;
    }

    // ── REGISTER ──────────────────────────────────────────────
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        // Use _db directly for register because user doesn't
        // have a tenant yet — repository tenant filtering
        // would not work here (no JWT = no tenantId in context)
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == req.Email);

        if (emailExists)
            return Conflict(new { message = "Email already registered." });

        var tenant = new Tenant
        {
            Name = req.CompanyName,
            Plan = "free"
        };
        _db.Tenants.Add(tenant);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = "admin"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(await BuildAuthResponse(user, tenant));
    }

    // ── LOGIN ─────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        // GetByEmailAsync does not filter by tenant
        // because we don't know the tenant until we find the user
        var user = await _userRepository.GetByEmailAsync(req.Email);

        var passwordValid = user is not null &&
            BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);

        if (!passwordValid || user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(await BuildAuthResponse(user, user.Tenant));
    }

    // ── REFRESH ───────────────────────────────────────────────
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(req.AccessToken);
        if (principal is null)
            return Unauthorized(new { message = "Invalid access token." });

        var userId = Guid.Parse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Use _db directly here because refresh endpoint has no JWT
        // (the access token is expired — TenantContext is not populated)
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null
            || user.RefreshToken != req.RefreshToken
            || user.RefreshTokenExpiry < DateTime.UtcNow)
            return Unauthorized(
                new { message = "Invalid or expired refresh token." });

        return Ok(await BuildAuthResponse(user, user.Tenant));
    }

    /*
    // ── GOOGLE CALLBACK ───────────────────────────────────────
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var properties =
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleCallback))
            };
        return Challenge(properties, "Google");
    }
    */
    
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext
            .AuthenticateAsync("Google");

        if (!result.Succeeded)
            return BadRequest(
                new { message = "Google authentication failed." });

        var email = result.Principal
            .FindFirstValue(ClaimTypes.Email);
        var name = result.Principal
            .FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(email))
            return BadRequest(
                new { message = "Could not get email from Google." });

        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null)
        {
            var tenant = new Tenant
            {
                Name = name ?? email.Split('@')[0],
                Plan = "free"
            };
            _db.Tenants.Add(tenant);

            user = new User
            {
                TenantId = tenant.Id,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                    Guid.NewGuid().ToString()),
                Role = "admin"
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            user = await _userRepository.GetByEmailAsync(email);
        }

        var accessToken = _tokenService
            .GenerateAccessToken(user!, user!.Tenant);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        var frontendUrl =
            $"http://localhost:5173/auth/callback" +
            $"?accessToken={accessToken}" +
            $"&refreshToken={refreshToken}";

        return Redirect(frontendUrl);
    }

    // ── HELPER ────────────────────────────────────────────────
    private async Task<AuthResponse> BuildAuthResponse(
        User user, Tenant tenant)
    {
        var accessToken =
            _tokenService.GenerateAccessToken(user, tenant);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            Email: user.Email,
            Role: user.Role,
            TenantName: tenant.Name);
    }
    
    // Public endpoint — no auth required
// Returns list of all tenants for storefront selector
    [HttpGet("tenants")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await _db.Tenants
            .Select(t => new
            {
                t.Id,
                t.Name
            })
            .OrderBy(t => t.Name)
            .ToListAsync();

        return Ok(tenants);
    }
    
    // Issues a guest token for a specific tenant
// Used by the storefront simulator
// No user credentials needed — just tenant selection
    [HttpPost("storefront-token")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStorefrontToken(
        [FromBody] StorefrontTokenRequest req)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == req.TenantId);

        if (tenant is null)
            return BadRequest(new { message = "Tenant not found." });

        // Check if storefront user already exists for this tenant
        var storefrontEmail =
            $"storefront@{tenant.Name.ToLower().Replace(" ", "")}.com";

        var storefrontUser = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Email == storefrontEmail &&
                u.TenantId == tenant.Id);

        // If not — create and save it
        if (storefrontUser is null)
        {
            storefrontUser = new User
            {
                Id = Guid.NewGuid(),
                Email = storefrontEmail,
                PasswordHash = BCrypt.Net.BCrypt
                    .HashPassword(Guid.NewGuid().ToString()),
                Role = "storefront",
                TenantId = tenant.Id
            };

            _db.Users.Add(storefrontUser);
            await _db.SaveChangesAsync();
        }

        var accessToken = _tokenService
            .GenerateAccessToken(storefrontUser, tenant);

        return Ok(new
        {
            accessToken,
            tenantName = tenant.Name,
            tenantId = tenant.Id
        });
    }
    public record StorefrontTokenRequest(Guid TenantId);
    
    
    // ── TEST ENDPOINT ─────────────────────────────────────────────
// GET /api/auth/me
// [Authorize] means: JWT required to access this endpoint
// If no token → 401 Unauthorized automatically
// If valid token → runs the method
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult Me(
        [FromServices] ITenantContext tenantContext)
    {
        // If you reach here — JWT was valid and TenantMiddleware ran
        // tenantContext is populated with data from the JWT
        return Ok(new
        {
            message = "Middleware is working",
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue(ClaimTypes.Email),
            tenantId = tenantContext.TenantId,
            tenantName = tenantContext.TenantName,
            plan = tenantContext.Plan,
            role = User.FindFirstValue(ClaimTypes.Role)
        });
    }
}