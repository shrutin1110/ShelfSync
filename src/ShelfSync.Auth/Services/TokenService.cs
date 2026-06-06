using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShelfSync.Auth.Settings;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Auth.Services;

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    // IOptions<JwtSettings> is how you receive configuration in a service
    // .NET reads appsettings.json, fills JwtSettings class,
    // and injects it here automatically
    // You never call "new TokenService()" yourself
    public TokenService(IOptions<JwtSettings> settings)
    {
        // .Value unwraps the IOptions wrapper to get the actual JwtSettings object
        _settings = settings.Value;
    }

    public string GenerateAccessToken(User user, Tenant tenant)
    {
        // Step 1: Create the signing key
        // We take our secret string and convert it to bytes
        // SymmetricSecurityKey wraps those bytes into a key object
        // "symmetric" means the same key signs AND verifies the token
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SecretKey));

        // Step 2: Create signing credentials
        // This combines the key with the algorithm
        // HmacSha256 is the standard algorithm for JWT signing
        // Think of it as: key + algorithm = signing pen
        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        // Step 3: Define the claims (the data inside the token)
        // Claims are key-value pairs stored in the token payload
        // Anyone with the token can READ these (not secret)
        // But nobody can CHANGE them without breaking the signature
        //
        // We put tenantId, role, email in here so that
        // ANY service can read who this user is just from the token
        // No database call needed
        var claims = new[]
        {
            // Standard .NET claim type for user ID
            // ClaimTypes.NameIdentifier = "http://schemas.xmlsoap.org/..."
            // but it's just a string label for the user's ID
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),

            // User's email address
            new Claim(ClaimTypes.Email, user.Email),

            // User's role — "admin", "seller", "viewer"
            // ClaimTypes.Role is special — .NET's [Authorize(Roles="admin")]
            // reads this claim automatically
            new Claim(ClaimTypes.Role, user.Role),

            // Custom claims — our own data we need in every service
            // tenantId tells every service which company this user belongs to
            new Claim("tenantId", tenant.Id.ToString()),

            // Nice to have — the company name for display purposes
            new Claim("tenantName", tenant.Name),

            // Which pricing plan — used to restrict features
            new Claim("plan", tenant.Plan),
        };

        // Step 4: Create the token object
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,       // who created it
            audience: _settings.Audience,   // who it's for
            claims: claims,                 // the data payload
            expires: DateTime.UtcNow.AddMinutes(
                _settings.AccessTokenExpiryMinutes), // when it expires
            signingCredentials: credentials  // how it's signed
        );

        // Step 5: Convert the token object to a string
        // This produces the "xxxxx.yyyyy.zzzzz" format
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // A refresh token is NOT a JWT
        // It is just a long random string stored in the database
        // When the client sends it back, we look it up in the DB
        // and verify it matches — then issue a new access token

        // 64 random bytes = 512 bits of randomness
        // Impossible to guess
        // Convert to base64 to make it a readable string
        var bytes = new byte[64];

        // RandomNumberGenerator is cryptographically secure
        // Do NOT use Random class for security-sensitive values
        // Random is predictable — RandomNumberGenerator is not
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        // This method reads claims out of an expired token
        // We use this during refresh — the access token expired
        // but we still need to know WHO is asking for a new one

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SecretKey));

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,

            // We skip issuer and audience validation here
            // because we just want to extract claims
            ValidateIssuer = false,
            ValidateAudience = false,

            // THIS IS THE MOST IMPORTANT LINE HERE
            // We set ValidateLifetime to FALSE
            // because the whole point of this method is to read
            // an ALREADY EXPIRED token
            // We WANT to read expired tokens here — that is expected
            ValidateLifetime = false
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();

            // ValidateToken reads the token and returns a ClaimsPrincipal
            // ClaimsPrincipal is an object that holds all the claims
            // Think of it as a bag containing all the user's info from the token
            // "out _" discards the raw SecurityToken object — we don't need it
            return handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            // Token is completely invalid — wrong format, wrong key
            // Not just expired — actually broken
            // Return null — the controller will reject the request
            return null;
        }
    }
}