using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface ISettingsRepository
{
    Task<IEnumerable<AppSettingDto>> GetAllSettingsAsync();
    Task<AppSettingDto?> GetSettingByKeyAsync(string key);
    Task<ApiResponse> UpdateSettingAsync(string key, string value, bool isActive = true);
    Task LogSecurityEventAsync(SecurityLogDto log);
}
