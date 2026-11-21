using System.Collections.Concurrent;
using Dapper;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class SettingsRepository : BaseRepository, ISettingsRepository
{
    // High-performance thread-safe in-memory cache for app settings (avoids round-trip remote DB on every page hit)
    private static readonly ConcurrentDictionary<string, (AppSettingDto? Setting, DateTime Expiry)> _settingsCache = new();

    public SettingsRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<IEnumerable<AppSettingDto>> GetAllSettingsAsync()
    {
        return await QuerySPAsync<AppSettingDto>("dbo.usp_AppSettings_Get");
    }

    public async Task<AppSettingDto?> GetSettingByKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        // Check cache (valid for 15 minutes)
        if (_settingsCache.TryGetValue(key, out var cached) && DateTime.UtcNow < cached.Expiry)
        {
            return cached.Setting;
        }

        var setting = await QueryFirstOrDefaultSPAsync<AppSettingDto>("dbo.usp_AppSettings_Get", new { SettingKey = key });
        _settingsCache[key] = (setting, DateTime.UtcNow.AddMinutes(15));
        return setting;
    }

    public async Task<ApiResponse> UpdateSettingAsync(string key, string value, bool isActive = true)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_AppSettings_Update", new
        {
            SettingKey = key,
            SettingValue = value,
            IsActive = isActive
        });

        // Invalidate cache immediately on update
        _settingsCache.TryRemove(key, out _);

        if (result != null && (int)result.Success == 1)
        {
            return ApiResponse.Ok((string)result.Message);
        }
        return ApiResponse.Fail("Failed to update setting");
    }

    public async Task LogSecurityEventAsync(SecurityLogDto log)
    {
        await ExecuteSPAsync("dbo.usp_Security_LogEvent", new
        {
            UserId = log.UserId,
            EventAction = log.EventAction,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent
        });
    }
}
