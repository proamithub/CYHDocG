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

    public async Task<IEnumerable<DoctorReelDto>> GetReelsFeedAsync(long? currentUserId, int pageNumber = 1, int pageSize = 15)
    {
        try
        {
            var reels = (await QuerySPAsync<DoctorReelDto>("dbo.usp_Doctor_GetReelsFeed", new
            {
                CurrentUserId = currentUserId,
                PageNumber = pageNumber,
                PageSize = pageSize
            })).ToList();

            if (reels.Any())
            {
                return reels;
            }
        }
        catch { }

        using var connection = CreateConnection();
        string fallbackSql = @"
            SELECT 
                d.DoctorId,
                u.FullName AS DoctorName,
                u.AvatarUrl AS DoctorPhoto,
                d.Specialty,
                d.Department,
                d.Qualification,
                d.ExperienceYears,
                d.ConsultationFeePerMinute,
                d.IsFreeConsultation,
                d.IsLive,
                d.LiveStreamUrl,
                d.ScheduledLiveAt,
                d.FollowerCount,
                d.Rating,
                ls.StreamId,
                ISNULL(ls.CurrentViewers, 0) AS CurrentViewers,
                ISNULL(ls.TotalLikes, 0) AS TotalLikes,
                CASE WHEN df.FollowerId IS NOT NULL THEN 1 ELSE 0 END AS IsFollowing,
                CASE WHEN ll.LikeId IS NOT NULL THEN 1 ELSE 0 END AS HasLiked
            FROM dbo.Doctors d
            INNER JOIN dbo.Users u ON d.UserId = u.UserId
            LEFT JOIN dbo.LiveStreams ls ON d.DoctorId = ls.DoctorId AND ls.IsActive = 1
            LEFT JOIN dbo.DoctorFollowers df ON d.DoctorId = df.DoctorId AND df.UserId = @CurrentUserId
            LEFT JOIN dbo.LiveStreamLikes ll ON ls.StreamId = ll.StreamId AND ll.UserId = @CurrentUserId
            WHERE d.IsApproved = 1 AND d.IsLive = 1 AND u.IsActive = 1
            ORDER BY ISNULL(ls.CurrentViewers, 0) DESC, d.Rating DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;";

        int offset = (pageNumber - 1) * pageSize;
        return await connection.QueryAsync<DoctorReelDto>(fallbackSql, new
        {
            CurrentUserId = currentUserId,
            Offset = offset,
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

    public async Task<ApiResponse<DoctorFollowResultDto>> ToggleFollowAsync(long doctorId, long userId)
    {
        try
        {
            using var connection = CreateConnection();
            var isFollowing = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM dbo.DoctorFollowers WHERE DoctorId = @DoctorId AND UserId = @UserId",
                new { DoctorId = doctorId, UserId = userId }) > 0;

            if (isFollowing)
            {
                await connection.ExecuteAsync(@"
                    DELETE FROM dbo.DoctorFollowers WHERE DoctorId = @DoctorId AND UserId = @UserId;
                    UPDATE dbo.Doctors SET FollowerCount = CASE WHEN FollowerCount > 0 THEN FollowerCount - 1 ELSE 0 END WHERE DoctorId = @DoctorId;",
                    new { DoctorId = doctorId, UserId = userId });
            }
            else
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO dbo.DoctorFollowers (DoctorId, UserId, CreatedAt) VALUES (@DoctorId, @UserId, SYSUTCDATETIME());
                    UPDATE dbo.Doctors SET FollowerCount = FollowerCount + 1 WHERE DoctorId = @DoctorId;",
                    new { DoctorId = doctorId, UserId = userId });
            }

            int newCount = await connection.ExecuteScalarAsync<int>(@"
                SELECT ISNULL(FollowerCount, 0) FROM dbo.Doctors WHERE DoctorId = @DoctorId",
                new { DoctorId = doctorId });

            var data = new DoctorFollowResultDto
            {
                IsFollowing = !isFollowing,
                FollowerCount = newCount,
                Message = !isFollowing ? "Followed doctor" : "Unfollowed doctor"
            };
            return ApiResponse<DoctorFollowResultDto>.Ok(data, data.Message);
        }
        catch (Exception ex)
        {
            return ApiResponse<DoctorFollowResultDto>.Fail("Follow action failed: " + ex.Message);
        }
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

    public async Task<ApiResponse> UnblockUserAsync(long doctorId, long userId)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Doctor_UnblockUser", new
        {
            DoctorId = doctorId,
            UserId = userId
        });

        if (result != null && (int)result.Success == 1)
        {
            return ApiResponse.Ok((string)result.Message);
        }
        return ApiResponse.Fail("Unable to unblock user");
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

    public async Task<bool> IsDoctorUserOwnerAsync(long doctorId, long callerUserId)
    {
        if (doctorId <= 0 || callerUserId <= 0) return false;

        using var connection = CreateConnection();
        int count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Doctors WHERE DoctorId = @DoctorId AND UserId = @UserId",
            new { DoctorId = doctorId, UserId = callerUserId });
        return count > 0;
    }

    public async Task<ApiResponse<LiveCommentDto>> AddStreamCommentAsync(long streamId, long userId, string message)
    {
        long resolvedStreamId = await ResolveActiveStreamIdAsync(streamId);
        if (resolvedStreamId <= 0)
        {
            return ApiResponse<LiveCommentDto>.Fail("Live stream is currently offline. Comments are closed.");
        }

        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_LiveStream_AddComment", new
        {
            StreamId = resolvedStreamId,
            UserId = userId,
            Message = message
        });

        if (result != null && (int)result.Success == 1)
        {
            var comment = new LiveCommentDto
            {
                CommentId = (long)result.CommentId,
                UserId = userId,
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
        long resolvedStreamId = await ResolveActiveStreamIdAsync(streamId);
        if (resolvedStreamId <= 0)
        {
            return Enumerable.Empty<LiveCommentDto>();
        }
        return await QuerySPAsync<LiveCommentDto>("dbo.usp_LiveStream_GetRecentComments", new { StreamId = resolvedStreamId });
    }

    public async Task<ApiResponse> DeleteStreamCommentAsync(long doctorId, long commentId, bool isAdmin = false)
    {
        if (commentId <= 0) return ApiResponse.Fail("Invalid comment");

        using var connection = CreateConnection();
        int rows = await connection.ExecuteAsync(@"
            DELETE c
            FROM dbo.LiveStreamComments c
            INNER JOIN dbo.LiveStreams s ON c.StreamId = s.StreamId
            WHERE c.CommentId = @CommentId
              AND (@IsAdmin = 1 OR s.DoctorId = @DoctorId);",
            new { CommentId = commentId, DoctorId = doctorId, IsAdmin = isAdmin ? 1 : 0 });

        return rows > 0
            ? ApiResponse.Ok("Comment deleted")
            : ApiResponse.Fail("Comment not found or unauthorized");
    }

    public async Task<ApiResponse<bool>> ToggleStreamLikeAsync(long streamId, long userId)
    {
        long resolvedStreamId = await ResolveActiveStreamIdAsync(streamId);
        if (resolvedStreamId <= 0)
        {
            return ApiResponse<bool>.Fail("Live stream is currently offline.");
        }

        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_LiveStream_ToggleLike", new
        {
            StreamId = resolvedStreamId,
            UserId = userId
        });

        if (result != null)
        {
            bool hasLiked = (int)result.HasLiked == 1;
            return ApiResponse<bool>.Ok(hasLiked, (string)result.Message);
        }
        return ApiResponse<bool>.Fail("Like failed");
    }

    private async Task<long> ResolveActiveStreamIdAsync(long streamOrDoctorId)
    {
        using var connection = CreateConnection();
        var activeStreamId = await connection.ExecuteScalarAsync<long?>(@"
            SELECT TOP 1 s.StreamId 
            FROM dbo.LiveStreams s
            LEFT JOIN dbo.Doctors d ON s.DoctorId = d.DoctorId
            WHERE (s.DoctorId = @Id OR s.StreamId = @Id OR d.UserId = @Id) 
              AND s.IsActive = 1 
            ORDER BY s.StreamId DESC",
            new { Id = streamOrDoctorId });

        if (activeStreamId.HasValue && activeStreamId.Value > 0)
        {
            return activeStreamId.Value;
        }

        return 0;
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

    public async Task<IEnumerable<DoctorAdminListDto>> GetAllDoctorsForAdminAsync()
    {
        using var connection = CreateConnection();
        string sql = @"
            SELECT 
                ISNULL(d.DoctorId, 0) AS DoctorId,
                u.UserId,
                u.FullName,
                u.Email,
                ISNULL(u.Phone, '') AS Phone,
                ISNULL(d.Specialty, 'General Physician') AS Specialty,
                ISNULL(d.Department, 'General Medicine') AS Department,
                ISNULL(d.Qualification, 'MBBS, MD') AS Qualification,
                ISNULL(d.ExperienceYears, 5) AS ExperienceYears,
                ISNULL(d.ConsultationFeePerMinute, 25.00) AS ConsultationFeePerMinute,
                ISNULL(d.IsFreeConsultation, 0) AS IsFreeConsultation,
                ISNULL(d.IsLive, 0) AS IsLive,
                ISNULL(d.IsApproved, 1) AS IsApproved,
                ISNULL(d.Rating, 5.0) AS Rating,
                ISNULL(d.FollowerCount, 0) AS FollowerCount,
                ISNULL(d.CreatedAt, u.CreatedAt) AS CreatedAt
            FROM dbo.Users u
            LEFT JOIN dbo.Doctors d ON u.UserId = d.UserId
            WHERE u.RoleId = 2
            ORDER BY u.UserId ASC;";
        return await connection.QueryAsync<DoctorAdminListDto>(sql);
    }

    public async Task<ApiResponse> ToggleDoctorApprovalAsync(long doctorId, bool isApproved)
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync("UPDATE dbo.Doctors SET IsApproved = @IsApproved WHERE DoctorId = @DoctorId", new
        {
            DoctorId = doctorId,
            IsApproved = isApproved ? 1 : 0
        });
        return ApiResponse.Ok(isApproved ? "Doctor account approved" : "Doctor approval revoked");
    }

    public async Task<AdminDashboardMetricsDto> GetAdminDashboardMetricsAsync()
    {
        using var connection = CreateConnection();
        var metrics = new AdminDashboardMetricsDto();
        try
        {
            string sql = @"
                SELECT 
                    (SELECT COUNT(*) FROM dbo.Users WHERE RoleId = 2) AS TotalDoctors,
                    (SELECT COUNT(*) FROM dbo.Doctors WHERE IsLive = 1) AS LiveDoctors,
                    (SELECT COUNT(*) FROM dbo.Consultations WHERE CreatedAt >= CAST(SYSUTCDATETIME() AS DATE)) AS ConsultationsToday,
                    (SELECT ISNULL(SUM(PlatformFee), 0.00) FROM dbo.PlatformLedger WHERE TransactionType = 'ConsultationDebit') AS PlatformRevenue,
                    (SELECT COUNT(*) FROM dbo.Prescriptions) AS PrescriptionsIssued,
                    (SELECT COUNT(*) FROM dbo.MedicineReminders WHERE Status = 'Pending' AND ReminderDateTime >= CAST(SYSUTCDATETIME() AS DATE)) AS RemindersDue;";

            var row = await connection.QueryFirstOrDefaultAsync<dynamic>(sql);
            if (row != null)
            {
                metrics.TotalDoctors = (int)(row.TotalDoctors ?? 0);
                metrics.LiveDoctors = (int)(row.LiveDoctors ?? 0);
                metrics.ConsultationsToday = (int)(row.ConsultationsToday ?? 0);
                metrics.PlatformRevenue = (decimal)(row.PlatformRevenue ?? 0.00m);
                metrics.PrescriptionsIssued = (int)(row.PrescriptionsIssued ?? 0);
                metrics.RemindersDue = (int)(row.RemindersDue ?? 0);
            }
        }
        catch
        {
            // Return zeroed metrics rather than fabricated numbers if DB is unavailable
        }
        return metrics;
    }

    public async Task<DoctorStudioMetricsDto?> GetDoctorStudioMetricsAsync(long doctorUserId)
    {
        using var connection = CreateConnection();
        try
        {
            string sql = @"
                SELECT TOP 1
                    ISNULL(d.DoctorId, 0) AS DoctorId,
                    u.FullName AS DoctorName,
                    ISNULL(d.Specialty, 'Specialist Physician') AS Specialty,
                    ISNULL(d.FollowerCount, 0) AS FollowerCount,
                    ISNULL(d.ConsultationFeePerMinute, 30.00) AS ConsultationFeePerMinute,
                    ISNULL(d.IsFreeConsultation, 0) AS IsFreeConsultation,
                    ISNULL(d.IsLive, 0) AS IsLive,
                    ISNULL(d.IsApproved, 0) AS IsApproved,
                    ISNULL(dw.Balance, 0.00) AS WalletBalance,
                    (SELECT COUNT(*) FROM dbo.Consultations c WHERE c.DoctorId = d.DoctorId AND c.CreatedAt >= CAST(SYSUTCDATETIME() AS DATE)) AS ConsultationsToday
                FROM dbo.Users u
                LEFT JOIN dbo.Doctors d ON u.UserId = d.UserId
                LEFT JOIN dbo.DoctorWallets dw ON d.DoctorId = dw.DoctorId
                WHERE u.UserId = @UserId;";

            return await connection.QueryFirstOrDefaultAsync<DoctorStudioMetricsDto>(sql, new { UserId = doctorUserId });
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResponse> SetDoctorLiveStatusAsync(long doctorIdOrUserId, bool isLive, string? liveStreamUrl)
    {
        using var connection = CreateConnection();
        try
        {
            long doctorId = await connection.ExecuteScalarAsync<long>(@"
                SELECT TOP 1 DoctorId FROM dbo.Doctors WHERE DoctorId = @Id OR UserId = @Id",
                new { Id = doctorIdOrUserId });

            if (doctorId == 0)
            {
                var user = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT UserId, FullName FROM dbo.Users WHERE UserId = @Id AND RoleId = 2",
                    new { Id = doctorIdOrUserId });

                if (user != null)
                {
                    doctorId = await connection.ExecuteScalarAsync<long>(@"
                        INSERT INTO dbo.Doctors (UserId, Department, Specialty, Qualification, ExperienceYears, ConsultationFeePerMinute, IsApproved, IsLive, LiveStreamUrl, CreatedAt)
                        VALUES (@UserId, 'General Medicine', 'General Physician & Consultant', 'MBBS, MD', 5, 25.00, 1, @IsLive, @Url, SYSUTCDATETIME());
                        SELECT SCOPE_IDENTITY();",
                        new { UserId = (long)user.UserId, IsLive = isLive ? 1 : 0, Url = liveStreamUrl });
                }
            }

            if (doctorId > 0)
            {
                await connection.ExecuteAsync(@"
                    UPDATE dbo.Doctors 
                    SET IsLive = @IsLive, 
                        LiveStreamUrl = @Url,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE DoctorId = @DoctorId",
                    new { DoctorId = doctorId, IsLive = isLive ? 1 : 0, Url = liveStreamUrl });

                if (isLive)
                {
                    var existingStreamId = await connection.ExecuteScalarAsync<long?>(@"
                        SELECT TOP 1 StreamId FROM dbo.LiveStreams WHERE DoctorId = @DoctorId AND IsActive = 1 ORDER BY StreamId DESC",
                        new { DoctorId = doctorId });

                    if (existingStreamId == null || existingStreamId == 0)
                    {
                        var docInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                            SELECT u.FullName, d.Specialty FROM dbo.Doctors d INNER JOIN dbo.Users u ON d.UserId = u.UserId WHERE d.DoctorId = @DoctorId",
                            new { DoctorId = doctorId });

                        string title = docInfo != null ? $"{docInfo.FullName} - Live Consultation ({docInfo.Specialty})" : "Live Health Consultation";
                        string playbackUrl = liveStreamUrl ?? "/Doctor/LiveStudio";

                        await connection.ExecuteAsync(@"
                            INSERT INTO dbo.LiveStreams (DoctorId, Title, PlaybackUrl, StartedAt, IsActive, CurrentViewers, TotalLikes, CreatedAt)
                            VALUES (@DoctorId, @Title, @PlaybackUrl, SYSUTCDATETIME(), 1, 0, 0, SYSUTCDATETIME());",
                            new { DoctorId = doctorId, Title = title, PlaybackUrl = playbackUrl });
                    }
                    else
                    {
                        await connection.ExecuteAsync(@"
                            UPDATE dbo.LiveStreams 
                            SET StartedAt = ISNULL(StartedAt, SYSUTCDATETIME()) 
                            WHERE StreamId = @StreamId",
                            new { StreamId = existingStreamId });
                    }
                }
                else
                {
                    await connection.ExecuteAsync(@"
                        UPDATE dbo.LiveStreams 
                        SET IsActive = 0, EndedAt = SYSUTCDATETIME(), CurrentViewers = 0 
                        WHERE DoctorId = @DoctorId AND IsActive = 1",
                        new { DoctorId = doctorId });
                }

                return ApiResponse.Ok(isLive ? "Doctor stream is now LIVE" : "Doctor stream ended");
            }

            return ApiResponse.Fail("Doctor profile not found");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail("Failed to set live status: " + ex.Message);
        }
    }

    public async Task UpdateStreamViewerCountAsync(long doctorOrStreamId, int currentViewers)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(@"
                UPDATE dbo.LiveStreams 
                SET CurrentViewers = @Count 
                WHERE (StreamId = @Id OR DoctorId = @Id) AND IsActive = 1",
                new { Id = doctorOrStreamId, Count = currentViewers });
        }
        catch { }
    }

    public async Task<IEnumerable<LiveStreamSessionDto>> GetDoctorLiveSessionsAsync(long doctorId, DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null)
    {
        using var connection = CreateConnection();
        string sql = @"
            SELECT 
                s.StreamId,
                s.DoctorId,
                s.Title,
                s.StartedAt,
                s.EndedAt,
                s.IsActive,
                s.CurrentViewers,
                s.TotalLikes,
                s.CreatedAt,
                (SELECT COUNT(1) FROM dbo.LiveStreamComments c WHERE c.StreamId = s.StreamId) AS TotalComments
            FROM dbo.LiveStreams s
            WHERE s.DoctorId = @DoctorId
              AND (@StartDate IS NULL OR s.CreatedAt >= @StartDate)
              AND (@EndDate IS NULL OR s.CreatedAt <= @EndDate)
              AND (@SearchTerm IS NULL OR s.Title LIKE '%' + @SearchTerm + '%')
            ORDER BY s.StreamId DESC;";

        return await connection.QueryAsync<LiveStreamSessionDto>(sql, new
        {
            DoctorId = doctorId,
            StartDate = startDate,
            EndDate = endDate,
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim()
        });
    }

    public async Task<IEnumerable<LiveCommentDto>> GetSessionCommentsAsync(long streamId, string? filterTerm = null)
    {
        using var connection = CreateConnection();
        string sql = @"
            SELECT 
                c.CommentId,
                c.UserId,
                c.Message,
                c.CreatedAt,
                u.FullName AS UserName,
                u.AvatarUrl
            FROM dbo.LiveStreamComments c
            INNER JOIN dbo.Users u ON c.UserId = u.UserId
            WHERE c.StreamId = @StreamId
              AND (@FilterTerm IS NULL OR u.FullName LIKE '%' + @FilterTerm + '%' OR c.Message LIKE '%' + @FilterTerm + '%')
            ORDER BY c.CreatedAt ASC;";

        return await connection.QueryAsync<LiveCommentDto>(sql, new
        {
            StreamId = streamId,
            FilterTerm = string.IsNullOrWhiteSpace(filterTerm) ? null : filterTerm.Trim()
        });
    }

    public async Task<LiveStreamSessionDto?> GetSessionDetailsAsync(long streamId)
    {
        using var connection = CreateConnection();
        string sql = @"
            SELECT 
                s.StreamId,
                s.DoctorId,
                s.Title,
                s.StartedAt,
                s.EndedAt,
                s.IsActive,
                s.CurrentViewers,
                s.TotalLikes,
                s.CreatedAt,
                (SELECT COUNT(1) FROM dbo.LiveStreamComments c WHERE c.StreamId = s.StreamId) AS TotalComments
            FROM dbo.LiveStreams s
            WHERE s.StreamId = @StreamId;";

        return await connection.QueryFirstOrDefaultAsync<LiveStreamSessionDto>(sql, new { StreamId = streamId });
    }

    private static async Task EnsurePayoutRequestsSchemaAsync(System.Data.IDbConnection connection)
    {
        string ensureSql = @"
            IF OBJECT_ID('dbo.DoctorPayoutRequests', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DoctorPayoutRequests (
                    PayoutRequestId BIGINT IDENTITY(1,1) PRIMARY KEY,
                    DoctorId BIGINT NOT NULL CONSTRAINT FK_PayoutReq_Doctor REFERENCES dbo.Doctors(DoctorId),
                    Amount DECIMAL(18,2) NOT NULL,
                    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
                    BankName NVARCHAR(100) NULL,
                    AccountNumber NVARCHAR(50) NULL,
                    IfscCode NVARCHAR(20) NULL,
                    UpiId NVARCHAR(100) NULL,
                    AdminRemarks NVARCHAR(300) NULL,
                    ProcessedAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END
            ELSE
            BEGIN
                IF COL_LENGTH('dbo.DoctorPayoutRequests', 'AdminRemarks') IS NULL
                    ALTER TABLE dbo.DoctorPayoutRequests ADD AdminRemarks NVARCHAR(300) NULL;
                IF COL_LENGTH('dbo.DoctorPayoutRequests', 'ProcessedAt') IS NULL
                    ALTER TABLE dbo.DoctorPayoutRequests ADD ProcessedAt DATETIME2 NULL;
            END";

        await connection.ExecuteAsync(ensureSql);
    }

    public async Task<DoctorEarningsSummaryDto> GetDoctorEarningsSummaryAsync(long doctorIdOrUserId)
    {
        using var connection = CreateConnection();
        try
        {
            await EnsurePayoutRequestsSchemaAsync(connection);

            long doctorId = doctorIdOrUserId;
            string resolveSql = @"
                SELECT TOP 1 d.DoctorId, u.FullName AS DoctorName
                FROM dbo.Doctors d
                INNER JOIN dbo.Users u ON d.UserId = u.UserId
                WHERE d.DoctorId = @Id OR d.UserId = @Id;";

            var docInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(resolveSql, new { Id = doctorIdOrUserId });
            if (docInfo != null)
            {
                doctorId = (long)docInfo.DoctorId;
            }

            string metricsSql = @"
                SELECT 
                    d.DoctorId,
                    u.FullName AS DoctorName,
                    ISNULL(dw.Balance, 0.00) AS AvailableBalance,
                    ISNULL(dw.TotalWithdrawn, 0.00) AS LifetimeWithdrawn,
                    ISNULL((
                        SELECT SUM(c.TotalAmount * 0.80) 
                        FROM dbo.Consultations c 
                        WHERE c.DoctorId = d.DoctorId AND c.Status = 'Completed' AND c.IsPaid = 1
                    ), 0.00) AS TotalNetEarned,
                    ISNULL((
                        SELECT COUNT(1) 
                        FROM dbo.Consultations c 
                        WHERE c.DoctorId = d.DoctorId 
                          AND c.Status = 'Completed'
                          AND c.CreatedAt >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)
                    ), 0) AS MonthSessionsCount,
                    ISNULL((
                        SELECT AVG(CAST(c.DurationSeconds AS FLOAT)) / 60.0 
                        FROM dbo.Consultations c 
                        WHERE c.DoctorId = d.DoctorId 
                          AND c.Status = 'Completed'
                          AND c.CreatedAt >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)
                    ), 0.0) AS MonthAvgDurationMinutes,
                    ISNULL((
                        SELECT SUM(c.TotalAmount) 
                        FROM dbo.Consultations c 
                        WHERE c.DoctorId = d.DoctorId 
                          AND c.Status = 'Completed'
                          AND c.CreatedAt >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)
                    ), 0.00) AS MonthGrossEarnings,
                    ISNULL((
                        SELECT SUM(c.TotalAmount * 0.80) 
                        FROM dbo.Consultations c 
                        WHERE c.DoctorId = d.DoctorId 
                          AND c.Status = 'Completed'
                          AND c.CreatedAt >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)
                    ), 0.00) AS MonthNetEarnings
                FROM dbo.Doctors d
                INNER JOIN dbo.Users u ON d.UserId = u.UserId
                LEFT JOIN dbo.DoctorWallets dw ON d.DoctorId = dw.DoctorId
                WHERE d.DoctorId = @DoctorId;";

            var summary = await connection.QueryFirstOrDefaultAsync<DoctorEarningsSummaryDto>(metricsSql, new { DoctorId = doctorId })
                          ?? new DoctorEarningsSummaryDto { DoctorId = doctorId };

            string consultationsSql = @"
                SELECT TOP 50
                    c.ConsultationId,
                    c.UserId AS PatientId,
                    ISNULL(u.FullName, 'Patient') AS PatientName,
                    u.AvatarUrl AS PatientAvatar,
                    c.StartedAt,
                    c.EndedAt,
                    c.CreatedAt,
                    ISNULL(c.DurationSeconds, 0) AS DurationSeconds,
                    ISNULL(c.RatePerMinute, 0.00) AS RatePerMinute,
                    ISNULL(c.TotalAmount, 0.00) AS GrossFee,
                    CASE WHEN c.IsPaid = 1 THEN c.TotalAmount * 0.80 ELSE 0.00 END AS DoctorNetCut,
                    c.IsPaid,
                    c.Status
                FROM dbo.Consultations c
                INNER JOIN dbo.Users u ON c.UserId = u.UserId
                WHERE c.DoctorId = @DoctorId
                ORDER BY c.ConsultationId DESC;";

            summary.RecentConsultations = (await connection.QueryAsync<DoctorConsultationSettlementDto>(consultationsSql, new { DoctorId = doctorId })).ToList();

            string payoutHistorySql = @"
                SELECT TOP 25
                    pr.PayoutRequestId,
                    pr.DoctorId,
                    pr.Amount,
                    pr.Status,
                    pr.BankName,
                    pr.AccountNumber,
                    pr.IfscCode,
                    pr.UpiId,
                    pr.AdminRemarks,
                    pr.CreatedAt,
                    pr.ProcessedAt
                FROM dbo.DoctorPayoutRequests pr
                WHERE pr.DoctorId = @DoctorId
                ORDER BY pr.PayoutRequestId DESC;";

            summary.PayoutHistory = (await connection.QueryAsync<DoctorPayoutItemDto>(payoutHistorySql, new { DoctorId = doctorId })).ToList();

            return summary;
        }
        catch
        {
            return new DoctorEarningsSummaryDto();
        }
    }

    public async Task<ApiResponse> RequestDoctorPayoutAsync(long doctorIdOrUserId, decimal amount, string? bankName, string? accountNumber, string? ifscCode, string? upiId)
    {
        if (amount < 100)
        {
            return ApiResponse.Fail("Minimum payout withdrawal amount is ₹100.00");
        }

        using var connection = CreateConnection();
        try
        {
            await EnsurePayoutRequestsSchemaAsync(connection);

            long doctorId = await connection.ExecuteScalarAsync<long>(@"
                SELECT TOP 1 d.DoctorId
                FROM dbo.Doctors d
                WHERE d.DoctorId = @Id OR d.UserId = @Id;", new { Id = doctorIdOrUserId });

            if (doctorId <= 0)
            {
                return ApiResponse.Fail("Doctor account not found");
            }

            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            using var tx = connection.BeginTransaction();
            try
            {
                // Atomically reserve/hold requested payout amount so duplicate requests cannot overdraw balance
                int updatedRows = await connection.ExecuteAsync(@"
                    UPDATE dbo.DoctorWallets
                    SET Balance = Balance - @Amount,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE DoctorId = @DoctorId
                      AND Balance >= @Amount;",
                    new { DoctorId = doctorId, Amount = amount },
                    tx);

                if (updatedRows == 0)
                {
                    decimal currentBalance = await connection.ExecuteScalarAsync<decimal?>(
                        "SELECT Balance FROM dbo.DoctorWallets WHERE DoctorId = @DoctorId",
                        new { DoctorId = doctorId },
                        tx) ?? 0.00m;

                    tx.Rollback();
                    return ApiResponse.Fail($"Insufficient wallet balance. Available: ₹{currentBalance:N2}");
                }

                await connection.ExecuteAsync(@"
                    INSERT INTO dbo.DoctorPayoutRequests (DoctorId, Amount, Status, BankName, AccountNumber, IfscCode, UpiId)
                    VALUES (@DoctorId, @Amount, 'Pending', @BankName, @AccountNumber, @IfscCode, @UpiId);",
                    new
                    {
                        DoctorId = doctorId,
                        Amount = amount,
                        BankName = bankName,
                        AccountNumber = accountNumber,
                        IfscCode = ifscCode,
                        UpiId = upiId
                    },
                    tx);

                tx.Commit();
                return ApiResponse.Ok("Payout request submitted and balance reserved for settlement. Processing time: 24-48 hours.");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail("Failed to submit payout request: " + ex.Message);
        }
    }

    public async Task<AdminLedgerPageDto> GetAdminLedgerDataAsync()
    {
        var result = new AdminLedgerPageDto();
        using var connection = CreateConnection();

        await EnsurePayoutRequestsSchemaAsync(connection);

        var totals = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT
                ISNULL((SELECT SUM(TotalAmount) FROM dbo.Consultations WHERE Status = 'Completed' AND IsPaid = 1), 0.00) AS GrossVolume,
                ISNULL((SELECT SUM(PlatformFee) FROM dbo.PlatformLedger WHERE TransactionType = 'ConsultationDebit'), 0.00) AS PlatformNet,
                ISNULL((SELECT SUM(TotalAmount * 0.80) FROM dbo.Consultations WHERE Status = 'Completed' AND IsPaid = 1), 0.00) AS DoctorCredited,
                ISNULL((SELECT SUM(Balance) FROM dbo.DoctorWallets), 0.00) AS DoctorAvail,
                ISNULL((SELECT SUM(TotalWithdrawn) FROM dbo.DoctorWallets), 0.00) AS TotalWithdrawn,
                ISNULL((SELECT COUNT(1) FROM dbo.DoctorPayoutRequests WHERE Status = 'Pending'), 0) AS PendingCount,
                ISNULL((SELECT SUM(Amount) FROM dbo.DoctorPayoutRequests WHERE Status = 'Pending'), 0.00) AS PendingAmount;");

        if (totals != null)
        {
            result.GrossConsultationVolume = (decimal)(totals.GrossVolume ?? 0.00m);
            result.PlatformNetRevenue = (decimal)(totals.PlatformNet ?? 0.00m);
            result.DoctorEarningsCredited = (decimal)(totals.DoctorCredited ?? 0.00m);
            result.DoctorAvailablePayoutBalance = (decimal)(totals.DoctorAvail ?? 0.00m);
            result.TotalDoctorWithdrawn = (decimal)(totals.TotalWithdrawn ?? 0.00m);
            result.PendingPayoutsCount = (int)(totals.PendingCount ?? 0);
            result.PendingPayoutsAmount = (decimal)(totals.PendingAmount ?? 0.00m);
        }

        result.Doctors = (await SearchDoctorsAsync("")).ToList();

        result.PayoutRequests = (await connection.QueryAsync<DoctorPayoutItemDto>(@"
            SELECT TOP 50
                pr.PayoutRequestId,
                pr.DoctorId,
                u.FullName AS DoctorName,
                u.Email AS DoctorEmail,
                pr.Amount,
                pr.Status,
                pr.BankName,
                pr.AccountNumber,
                pr.IfscCode,
                pr.UpiId,
                pr.AdminRemarks,
                pr.CreatedAt,
                pr.ProcessedAt
            FROM dbo.DoctorPayoutRequests pr
            INNER JOIN dbo.Doctors d ON pr.DoctorId = d.DoctorId
            INNER JOIN dbo.Users u ON d.UserId = u.UserId
            ORDER BY CASE WHEN pr.Status = 'Pending' THEN 0 ELSE 1 END, pr.PayoutRequestId DESC;")).ToList();

        result.RecentLedgerEntries = (await connection.QueryAsync<PlatformLedgerItemDto>(@"
            SELECT TOP 50
                l.LedgerId,
                l.TransactionType,
                fu.FullName AS FromUserName,
                du.FullName AS ToDoctorName,
                l.Amount,
                l.PlatformFee,
                l.ReferenceId,
                l.Description,
                l.CreatedAt
            FROM dbo.PlatformLedger l
            LEFT JOIN dbo.Users fu ON l.FromUserId = fu.UserId
            LEFT JOIN dbo.Doctors d ON l.ToDoctorId = d.DoctorId
            LEFT JOIN dbo.Users du ON d.UserId = du.UserId
            ORDER BY l.LedgerId DESC;")).ToList();

        return result;
    }

    public async Task<ApiResponse> ProcessDoctorPayoutAsync(long payoutRequestId, string action, string? adminRemarks)
    {
        if (payoutRequestId <= 0)
        {
            return ApiResponse.Fail("Invalid payout request ID");
        }

        bool isApprove = string.Equals(action, "Paid", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase);
        bool isReject = string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(action, "Rejected", StringComparison.OrdinalIgnoreCase);

        if (!isApprove && !isReject)
        {
            return ApiResponse.Fail("Unsupported payout action");
        }

        using var connection = CreateConnection();
        await EnsurePayoutRequestsSchemaAsync(connection);

        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var tx = connection.BeginTransaction();
        try
        {
            var req = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT PayoutRequestId, DoctorId, Amount, Status
                FROM dbo.DoctorPayoutRequests WITH (UPDLOCK, ROWLOCK)
                WHERE PayoutRequestId = @Id;",
                new { Id = payoutRequestId },
                tx);

            if (req == null)
            {
                tx.Rollback();
                return ApiResponse.Fail("Payout request not found");
            }

            if (!string.Equals((string)req.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                tx.Rollback();
                return ApiResponse.Fail($"Payout request is already {(string)req.Status}");
            }

            long doctorId = (long)req.DoctorId;
            decimal amount = (decimal)req.Amount;

            if (isApprove)
            {
                await connection.ExecuteAsync(@"
                    UPDATE dbo.DoctorPayoutRequests
                    SET Status = 'Paid',
                        ProcessedAt = SYSUTCDATETIME(),
                        AdminRemarks = @Remarks
                    WHERE PayoutRequestId = @Id;",
                    new { Id = payoutRequestId, Remarks = adminRemarks },
                    tx);

                await connection.ExecuteAsync(@"
                    IF EXISTS (SELECT 1 FROM dbo.DoctorWallets WHERE DoctorId = @DoctorId)
                        UPDATE dbo.DoctorWallets SET TotalWithdrawn = TotalWithdrawn + @Amount, UpdatedAt = SYSUTCDATETIME() WHERE DoctorId = @DoctorId;
                    ELSE
                        INSERT INTO dbo.DoctorWallets (DoctorId, Balance, TotalWithdrawn, UpdatedAt) VALUES (@DoctorId, 0.00, @Amount, SYSUTCDATETIME());",
                    new { DoctorId = doctorId, Amount = amount },
                    tx);

                await connection.ExecuteAsync(@"
                    INSERT INTO dbo.PlatformLedger (TransactionType, FromUserId, ToDoctorId, Amount, PlatformFee, ReferenceId, Description, CreatedAt)
                    VALUES ('DoctorPayout', NULL, @DoctorId, @Amount, 0.00, @RefId, @Desc, SYSUTCDATETIME());",
                    new
                    {
                        DoctorId = doctorId,
                        Amount = amount,
                        RefId = $"PAYOUT_{payoutRequestId}",
                        Desc = string.IsNullOrWhiteSpace(adminRemarks) ? "Doctor bank/UPI payout settled by Admin" : adminRemarks.Trim()
                    },
                    tx);

                tx.Commit();
                return ApiResponse.Ok($"Payout #{payoutRequestId} marked as Paid (₹{amount:N2}).");
            }
            else
            {
                await connection.ExecuteAsync(@"
                    UPDATE dbo.DoctorPayoutRequests
                    SET Status = 'Rejected',
                        ProcessedAt = SYSUTCDATETIME(),
                        AdminRemarks = @Remarks
                    WHERE PayoutRequestId = @Id;",
                    new { Id = payoutRequestId, Remarks = adminRemarks },
                    tx);

                // Refund held balance back to DoctorWallet
                await connection.ExecuteAsync(@"
                    UPDATE dbo.DoctorWallets
                    SET Balance = Balance + @Amount,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE DoctorId = @DoctorId;",
                    new { DoctorId = doctorId, Amount = amount },
                    tx);

                tx.Commit();
                return ApiResponse.Ok($"Payout #{payoutRequestId} rejected and ₹{amount:N2} refunded to doctor wallet.");
            }
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return ApiResponse.Fail("Failed to process payout: " + ex.Message);
        }
    }
}
