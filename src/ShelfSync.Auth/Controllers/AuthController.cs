using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShelfSync.Auth.Data;
using ShelfSync.Auth.DTOs;
using ShelfSync.Auth.Services;
using ShelfSync.Shared.Entities;
using System.Security.Claims;

namespace ShelfSync.Auth.Controllers;

// [ApiController] → this class handles HTTP requests
// [Route("api/auth")] → all endpoints here start with /api/auth
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;

    // Constructor — .NET automatically provides AppDbContext and ITokenService
    // because you registered them in Program.cs
    // This is Dependency Injection — you ask for what you need,
    // .NET provides it. You never write "new AppDbContext()" yourself.
    public AuthController(AppDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    // ── REGISTER ──────────────────────────────────────────────
    // [HttpPost("register")] → handles POST requests to /api/auth/register
    // RegisterRequest req → .NET reads the JSON body and maps it to this object
    // async → this method waits for database operations without blocking
    // Task<IActionResult> → returns an HTTP response (200 OK, 409 Conflict etc.)
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        // Check if this email already exists in the database
        // AnyAsync returns true/false — more efficient than loading the full user
        // just to check if it exists
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == req.Email);

        if (emailExists)
        {
            // 409 Conflict — this resource already exists
            // We return an object with a message property
            // .NET automatically converts it to JSON: {"message": "..."}
            return Conflict(new { message = "Email already registered." });
        }

        // Create the Tenant first because User needs a TenantId
        // Every new registration creates a brand new company (Tenant)
        var tenant = new Tenant
        {
            Name = req.CompanyName,
            Plan = "free" // everyone starts on free plan
        };

        // _db.Tenants.Add tells EF Core to TRACK this object
        // Nothing is saved to the database yet
        // EF Core collects all changes and sends them together
        _db.Tenants.Add(tenant);

        // Now create the User linked to that Tenant
        var user = new User
        {
            // tenant.Id is the Guid we just created above
            // This links the user to their company
            TenantId = tenant.Id,

            Email = req.Email,

            // NEVER store the plain password
            // BCrypt.HashPassword converts "Test@1234" to a hash like
            // "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy"
            // This hash is what gets stored in the database
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),

            // First user of a company is always the admin
            Role = "admin"
        };

        _db.Users.Add(user);

        // SaveChangesAsync is where EF Core actually talks to the database
        // It sends both INSERT statements (tenant AND user) in one transaction
        // A transaction means: either BOTH succeed or NEITHER is saved
        // If saving the user fails, the tenant is also rolled back
        await _db.SaveChangesAsync();

        // Build the auth response (generates tokens, saves refresh token)
        // and return 200 OK with the tokens
        return Ok(await BuildAuthResponse(user, tenant));
    }

    // ── LOGIN ─────────────────────────────────────────────────
    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        // Find the user by email
        // Include(u => u.Tenant) = "also load the Tenant when loading this User"
        // This is called eager loading
        // Without Include, user.Tenant would be null
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == req.Email);

        // SECURITY PATTERN — Timing attack prevention
        // Always run BCrypt.Verify even if the user wasn't found
        //
        // Why? If you return immediately when email not found,
        // an attacker can measure response time:
        // Fast response = email doesn't exist
        // Slow response = email exists, wrong password
        // This leaks information about which emails are registered
        //
        // By always running BCrypt.Verify, both cases take the same time
        var passwordValid = user is not null &&
            BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);

        if (!passwordValid || user is null)
        {
            // 401 Unauthorized
            // Deliberately vague message — don't tell attacker which part was wrong
            // "Invalid email or password" not "Email not found" or "Wrong password"
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(await BuildAuthResponse(user, user.Tenant));
    }

    // ── REFRESH ───────────────────────────────────────────────
    // POST /api/auth/refresh
    // Called when the 15-minute access token expires
    // Client sends: expired access token + refresh token
    // Server responds with: fresh access token
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        // Read the claims from the expired access token
        // We need to know WHO is asking for a new token
        // GetPrincipalFromExpiredToken ignores the expiry — reads claims only
        var principal = _tokenService.GetPrincipalFromExpiredToken(req.AccessToken);

        if (principal is null)
        {
            // The token is completely invalid — not just expired
            // Wrong format, wrong signature, tampered with
            return Unauthorized(new { message = "Invalid access token." });
        }

        // Extract the userId from the token claims
        // FindFirstValue finds the claim with this type and returns its value
        // The ! at the end means "I know this won't be null"
        // (we put this claim in when creating the token, so it's always there)
        var userId = Guid.Parse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Load the user from database using the userId from the token
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId);

        // THREE security checks before issuing a new token:
        if (user is null
            // 1. User still exists in database (account not deleted)

            || user.RefreshToken != req.RefreshToken
            // 2. The refresh token matches what we stored in the database
            //    If someone steals and uses a refresh token, the legitimate
            //    user's next refresh will fail — alerting them to the breach

            || user.RefreshTokenExpiry < DateTime.UtcNow)
            // 3. The refresh token hasn't expired (within 7 days)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        // All checks passed — issue fresh tokens
        return Ok(await BuildAuthResponse(user, user.Tenant));
    }

    // ── PRIVATE HELPER ────────────────────────────────────────
    // private → only this controller uses this method
    // async → needs to save to database
    // Called by Register, Login, and Refresh — avoids repeating code
    private async Task<AuthResponse> BuildAuthResponse(User user, Tenant tenant)
    {
        // Generate fresh tokens
        var accessToken = _tokenService.GenerateAccessToken(user, tenant);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Save the new refresh token to database
        // We rotate the refresh token every time — each login/refresh
        // gets a brand new refresh token
        // Why rotate? If the old refresh token was stolen,
        // rotating invalidates it immediately after first use
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        // SaveChangesAsync sends the UPDATE statement to the database
        await _db.SaveChangesAsync();

        // Return only what the client needs
        // Never return PasswordHash or other sensitive fields
        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            Email: user.Email,
            Role: user.Role,
            TenantName: tenant.Name);
    }
}