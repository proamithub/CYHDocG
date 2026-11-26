using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IDoctorRepository
{
    Task<IEnumerable<DoctorReelDto>> GetReelsFeedAsync(long? currentUserId, int pageNumber = 1, int pageSize = 10);
    Task<IEnumerable<DoctorSearchDto>> SearchDoctorsAsync(string searchTerm);
    Task<ApiResponse<DoctorFollowResultDto>> ToggleFollowAsync(long doctorId, long userId);
    Task<ApiResponse> BlockUserAsync(long doctorId, long userId, string reason);
    Task<ApiResponse> UnblockUserAsync(long doctorId, long userId);
    Task<bool> IsUserBlockedAsync(long doctorId, long userId);
    Task<bool> IsDoctorUserOwnerAsync(long doctorId, long callerUserId);
    Task<ApiResponse<LiveCommentDto>> AddStreamCommentAsync(long streamId, long userId, string message);
    Task<IEnumerable<LiveCommentDto>> GetRecentCommentsAsync(long streamId);
    Task<ApiResponse> DeleteStreamCommentAsync(long doctorId, long commentId, bool isAdmin = false);
    Task<ApiResponse<bool>> ToggleStreamLikeAsync(long streamId, long userId);
    Task<ApiResponse> UpdateDoctorPricingAsync(long doctorId, bool isFree, decimal feePerMinute);
    Task<IEnumerable<DoctorAdminListDto>> GetAllDoctorsForAdminAsync();
    Task<ApiResponse> ToggleDoctorApprovalAsync(long doctorId, bool isApproved);
    Task<AdminDashboardMetricsDto> GetAdminDashboardMetricsAsync();
    Task<DoctorStudioMetricsDto?> GetDoctorStudioMetricsAsync(long doctorUserId);
    Task<ApiResponse> SetDoctorLiveStatusAsync(long doctorId, bool isLive, string? liveStreamUrl);
    Task UpdateStreamViewerCountAsync(long doctorOrStreamId, int currentViewers);
    Task<IEnumerable<LiveStreamSessionDto>> GetDoctorLiveSessionsAsync(long doctorId, DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null);
    Task<IEnumerable<LiveCommentDto>> GetSessionCommentsAsync(long streamId, string? filterTerm = null);
    Task<LiveStreamSessionDto?> GetSessionDetailsAsync(long streamId);
    Task<DoctorEarningsSummaryDto> GetDoctorEarningsSummaryAsync(long doctorIdOrUserId);
    Task<ApiResponse> RequestDoctorPayoutAsync(long doctorIdOrUserId, decimal amount, string? bankName, string? accountNumber, string? ifscCode, string? upiId);
    Task<AdminLedgerPageDto> GetAdminLedgerDataAsync();
    Task<ApiResponse> ProcessDoctorPayoutAsync(long payoutRequestId, string action, string? adminRemarks);
}
