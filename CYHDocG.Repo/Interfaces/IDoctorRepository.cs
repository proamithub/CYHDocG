using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IDoctorRepository
{
    Task<IEnumerable<DoctorReelDto>> GetReelsFeedAsync(long? currentUserId, int pageNumber = 1, int pageSize = 10);
    Task<IEnumerable<DoctorSearchDto>> SearchDoctorsAsync(string searchTerm);
    Task<ApiResponse<bool>> ToggleFollowAsync(long doctorId, long userId);
    Task<ApiResponse> BlockUserAsync(long doctorId, long userId, string reason);
    Task<bool> IsUserBlockedAsync(long doctorId, long userId);
    Task<ApiResponse<LiveCommentDto>> AddStreamCommentAsync(long streamId, long userId, string message);
    Task<IEnumerable<LiveCommentDto>> GetRecentCommentsAsync(long streamId);
    Task<ApiResponse<bool>> ToggleStreamLikeAsync(long streamId, long userId);
    Task<ApiResponse> UpdateDoctorPricingAsync(long doctorId, bool isFree, decimal feePerMinute);
    Task<IEnumerable<DoctorAdminListDto>> GetAllDoctorsForAdminAsync();
    Task<ApiResponse> ToggleDoctorApprovalAsync(long doctorId, bool isApproved);
    Task<AdminDashboardMetricsDto> GetAdminDashboardMetricsAsync();
    Task<DoctorStudioMetricsDto?> GetDoctorStudioMetricsAsync(long doctorUserId);
    Task<ApiResponse> SetDoctorLiveStatusAsync(long doctorId, bool isLive, string? liveStreamUrl);
}
