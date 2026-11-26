using System.Text.Json;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CYHDocG.Repo.Implementations;

public class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly ISettingsRepository? _settingsRepo;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public GoogleTokenVerifier(
        IConfiguration configuration,
        ISettingsRepository? settingsRepo = null,
        HttpClient? httpClient = null)
    {
        _configuration = configuration;
        _settingsRepo = settingsRepo;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string?> ResolveClientIdAsync()
    {
        if (_settingsRepo != null)
        {
            try
            {
                var dbClientId = await _settingsRepo.GetSettingByKeyAsync("GoogleClientId");
                if (!string.IsNullOrWhiteSpace(dbClientId?.SettingValue) &&
                    !dbClientId.SettingValue.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                {
                    return dbClientId.SettingValue.Trim();
                }
            }
            catch { }
        }

        string? cfgId = _configuration["Authentication:Google:ClientId"] ?? _configuration["Google:ClientId"];
        if (!string.IsNullOrWhiteSpace(cfgId) && !cfgId.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            return cfgId.Trim();
        }

        return null;
    }

    public async Task<GoogleAuthDto?> VerifyIdTokenAsync(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        string? expectedClientId = await ResolveClientIdAsync();
        if (string.IsNullOrWhiteSpace(expectedClientId))
        {
            return null;
        }

        try
        {
            string url = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken.Trim())}";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? aud = root.TryGetProperty("aud", out var audProp) ? audProp.GetString() : null;
            if (!string.Equals(aud, expectedClientId, StringComparison.Ordinal))
            {
                return null;
            }

            bool emailVerified = false;
            if (root.TryGetProperty("email_verified", out var evProp))
            {
                emailVerified = evProp.ValueKind == JsonValueKind.True ||
                    (evProp.ValueKind == JsonValueKind.String && string.Equals(evProp.GetString(), "true", StringComparison.OrdinalIgnoreCase));
            }

            if (!emailVerified)
            {
                return null;
            }

            if (root.TryGetProperty("exp", out var expProp))
            {
                long expSeconds = expProp.ValueKind == JsonValueKind.Number
                    ? expProp.GetInt64()
                    : (long.TryParse(expProp.GetString(), out long parsed) ? parsed : 0);

                if (expSeconds > 0 && DateTimeOffset.FromUnixTimeSeconds(expSeconds) <= DateTimeOffset.UtcNow)
                {
                    return null;
                }
            }

            string? sub = root.TryGetProperty("sub", out var subProp) ? subProp.GetString() : null;
            string? email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            string fullName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                ? nameProp.GetString()!
                : email.Split('@')[0];

            string? avatarUrl = root.TryGetProperty("picture", out var picProp) ? picProp.GetString() : null;

            return new GoogleAuthDto
            {
                IdToken = idToken,
                GoogleId = sub,
                Email = email,
                FullName = fullName,
                AvatarUrl = avatarUrl
            };
        }
        catch
        {
            return null;
        }
    }
}
