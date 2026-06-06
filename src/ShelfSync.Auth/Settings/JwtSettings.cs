namespace ShelfSync.Auth.Settings;

// This class mirrors the "JwtSettings" section in appsettings.json

// .NET reads the JSON and automatically fills this class
public class JwtSettings
{
    // maps to "SecretKey" in JSON
    public string SecretKey { get; set; } = string.Empty;

    // maps to "Issuer" in JSON
    public string Issuer { get; set; } = string.Empty;

    // maps to "Audience" in JSON
    public string Audience { get; set; } = string.Empty;

    // maps to "AccessTokenExpiryMinutes" in JSON
    public int AccessTokenExpiryMinutes { get; set; }

    // maps to "RefreshTokenExpiryDays" in JSON
    public int RefreshTokenExpiryDays { get; set; }
}