using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class LiveKitTokenService : ILiveKitTokenService
{
    private readonly LiveKitSettings _settings;

    public LiveKitTokenService(IConfiguration configuration)
    {
        // Read dynamically from configuration (appsettings.json / appsettings.Production.json / environment)
        var section = configuration.GetSection(LiveKitSettings.SectionName);
        var apiKey = section["ApiKey"];
        var apiSecret = section["ApiSecret"];
        var liveKitUrl = section["LiveKitUrl"];

        // Support CyhAstro "LiveKit" section fallback
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            var fallback = configuration.GetSection("LiveKit");
            if (fallback.Exists())
            {
                apiKey = fallback["ApiKey"] ?? apiKey;
                apiSecret = fallback["ApiSecret"] ?? apiSecret;
                liveKitUrl = fallback["Url"] ?? fallback["LiveKitUrl"] ?? liveKitUrl;
            }
        }

        _settings = new LiveKitSettings
        {
            LiveKitUrl = !string.IsNullOrWhiteSpace(liveKitUrl) ? liveKitUrl : "wss://cyhastro-pimmehdb.livekit.cloud",
            ApiKey = apiKey ?? string.Empty,
            ApiSecret = apiSecret ?? string.Empty
        };
    }

    public string GetServerUrl() => _settings.LiveKitUrl;

    public string GenerateAccessToken(string roomName, string participantIdentity, string participantName, bool canPublish = true, bool canSubscribe = true)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiSecret) || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("LiveKit API credentials are not configured in appsettings.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.ApiSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var videoGrants = new Dictionary<string, object>
        {
            ["room"] = roomName,
            ["roomJoin"] = true,
            ["canPublish"] = canPublish,
            ["canSubscribe"] = canSubscribe,
            ["canPublishData"] = true
        };

        var claims = new List<Claim>
        {
            new("sub", participantIdentity),
            new("name", participantName),
            new("video", JsonSerializer.Serialize(videoGrants), JsonClaimValueTypes.Json)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.ApiKey,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-10),
            expires: DateTime.UtcNow.AddHours(6),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
