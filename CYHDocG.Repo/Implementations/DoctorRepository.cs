using Dapper;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class DoctorRepository : BaseRepository, IDoctorRepository
{
    public DoctorRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<IEnumerable<DoctorReelDto>> GetReelsFeedAsync(long? currentUserId, int pageNumber = 1, int pageSize = 10)
    {
        return await QuerySPAsync<DoctorReelDto>("dbo.usp_Doctor_GetReelsFeed", new
        {
            CurrentUserId = currentUserId,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
    }

    public async Task<IEnumerable<DoctorSearchDto>> SearchDoctorsAsync(string searchTerm)
    {
        return await QuerySPAsync<DoctorSearchDto>("dbo.usp_Doctor_SearchBySymptomOrDepartment", new
        {
            SearchTerm = searchTerm
        });
    }

    public async Task<ApiResponse<bool>> ToggleFollowAsync(long doctorId, long userId)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Doctor_ToggleFollow", new
        {
            DoctorId = doctorId,
            UserId = userId
        });

        if (result != null)
        {
            bool isFollowing = (int)result.IsFollowing == 1;
            return ApiResponse<bool>.Ok(isFollowing, (string)result.Message);
        }
        return ApiResponse<bool>.Fail("Action failed");
    }

    public async Task<ApiResponse> BlockUserAsync(long doctorId, long userId, string reason)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Doctor_BlockUser", new
        {
            DoctorId = doctorId,
            UserId = userId,
            Reason = reason
        });

        if (result != null && (int)result.Success == 1)
        {
            return ApiResponse.Ok((string)result.Message);
        }
        return ApiResponse.Fail("Unable to block user");
    }

    public async Task<bool> IsUserBlockedAsync(long doctorId, long userId)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Doctor_IsUserBlocked", new
        {
            DoctorId = doctorId,
            UserId = userId
        });

        return result != null && (int)result.IsBlocked == 1;
    }

    public async Task<ApiResponse<LiveCommentDto>> AddStreamCommentAsync(long streamId, long userId, string message)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_LiveStream_AddComment", new
        {
            StreamId = streamId,
            UserId = userId,
            Message = message
        });

        if (result != null && (int)result.Success == 1)
        {
            var comment = new LiveCommentDto
            {
                CommentId = (long)result.CommentId,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                UserName = (string)result.UserName,
                AvatarUrl = (string?)result.AvatarUrl
            };
            return ApiResponse<LiveCommentDto>.Ok(comment, "Comment added");
        }
        return ApiResponse<LiveCommentDto>.Fail(result != null ? (string)result.Message : "Comment failed");
    }

    public async Task<IEnumerable<LiveCommentDto>> GetRecentCommentsAsync(long streamId)
    {
        return await QuerySPAsync<LiveCommentDto>("dbo.usp_LiveStream_GetRecentComments", new { StreamId = streamId });
    }

    public async Task<ApiResponse<bool>> ToggleStreamLikeAsync(long streamId, long userId)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_LiveStream_ToggleLike", new
        {
            StreamId = streamId,
            UserId = userId
        });

        if (result != null)
        {
            bool hasLiked = (int)result.HasLiked == 1;
            return ApiResponse<bool>.Ok(hasLiked, (string)result.Message);
        }
        return ApiResponse<bool>.Fail("Like failed");
    }

    public async Task<ApiResponse> UpdateDoctorPricingAsync(long doctorId, bool isFree, decimal feePerMinute)
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync("UPDATE dbo.Doctors SET IsFreeConsultation = @IsFree, ConsultationFeePerMinute = @Fee WHERE DoctorId = @DoctorId", new
        {
            DoctorId = doctorId,
            IsFree = isFree ? 1 : 0,
            Fee = feePerMinute
        });
        return ApiResponse.Ok("Doctor service pricing updated successfully");
    }
}
