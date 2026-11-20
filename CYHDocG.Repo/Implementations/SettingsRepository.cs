using Dapper;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class SettingsRepository : BaseRepository, ISettingsRepository
{
    public SettingsRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<IEnumerable<AppSettingDto>> GetAllSettingsAsync()
    {
        return await QuerySPAsync<AppSettingDto>("dbo.usp_AppSettings_Get");
    }

    public async Task<AppSettingDto?> GetSettingByKeyAsync(string key)
    {
        return await QueryFirstOrDefaultSPAsync<AppSettingDto>("dbo.usp_AppSettings_Get", new { SettingKey = key });
    }

    public async Task<ApiResponse> UpdateSettingAsync(string key, string value, bool isActive = true)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_AppSettings_Update", new
        {
            SettingKey = key,
            SettingValue = value,
            IsActive = isActive
        });

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
