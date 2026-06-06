namespace ShelfSync.Auth.DTOs;

// "record" is a simplified class perfect for DTOs
// It is immutable (can't change after creation)
// The properties are defined in the parentheses
// .NET automatically creates them

// What the client sends when registering a new account
// Email and password for the user
// CompanyName creates a new Tenant
public record RegisterRequest(
    string Email,
    string Password,
    string CompanyName);

// What the client sends when logging in
public record LoginRequest(
    string Email,
    string Password);

// What the client sends when refreshing their token
// Needs BOTH tokens:
// AccessToken → to identify who the user is (even though it's expired)
// RefreshToken → to prove they are allowed to get a new access token
public record RefreshRequest(
    string AccessToken,
    string RefreshToken);

// What the server sends BACK after successful register/login/refresh
// Notice: no PasswordHash, no RefreshTokenExpiry, no sensitive DB fields
// Only what the client actually needs
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string Email,
    string Role,
    string TenantName);