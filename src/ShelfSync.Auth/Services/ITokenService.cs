using System.Security.Claims;
using ShelfSync.Shared.Entities;

namespace ShelfSync.Auth.Services;


public interface ITokenService
{
    // Given a user and their tenant, create a JWT access token
    // Returns the token as a string like "eyJhbGc..."
    string GenerateAccessToken(User user, Tenant tenant);

    // Create a random refresh token string
    // This is NOT a JWT — just a random secure string
    string GenerateRefreshToken();

    // Read claims out of an EXPIRED access token
    // Used during refresh — token is expired but we need to know who is asking
    // Returns null if the token is completely invalid (not just expired)
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}