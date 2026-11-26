using CYHDocG.Repo.DTOs;
using Microsoft.Extensions.Configuration;

namespace CYHDocG.Repo.Security;

/// <summary>
/// Resolves consistent JWT settings across API middleware and token generation.
/// </summary>
public static class JwtConfigResolver
{
    public static JwtConfig Resolve(IConfiguration configuration)
    {
        var config = new JwtConfig();

        ApplyConfig(
            configuration["Authentication:Jwt:SecretKey"],
            configuration["Authentication:Jwt:Issuer"],
            configuration["Authentication:Jwt:Audience"],
            configuration["Authentication:Jwt:ExpiryMinutes"]);

        ApplyConfig(
            configuration["Jwt:Key"],
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            configuration["Jwt:ExpiryMinutes"]);

        return config;

        void ApplyConfig(string? key, string? issuer, string? audience, string? expiry)
        {
            if (!string.IsNullOrWhiteSpace(key)) config.SecretKey = key;
            if (!string.IsNullOrWhiteSpace(issuer)) config.Issuer = issuer;
            if (!string.IsNullOrWhiteSpace(audience)) config.Audience = audience;
            if (int.TryParse(expiry, out int mins) && mins > 0) config.ExpiryMinutes = mins;
        }
    }
}
