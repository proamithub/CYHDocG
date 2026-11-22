-- ============================================================================
-- CYHDocGuru Stored Procedures: 03_Stored_Procedures_Doctor_And_LiveReels.sql
-- ============================================================================

IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. Doctor Reels Feed (Mobile-Style Vertical Scroll)
CREATE OR ALTER PROCEDURE dbo.usp_Doctor_GetReelsFeed
    @CurrentUserId BIGINT = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

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
    WHERE d.IsApproved = 1
    ORDER BY d.IsLive DESC, d.ScheduledLiveAt ASC, d.Rating DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- 2. Search Doctors by Symptom, Disease, or Department (AI Auto-Suggest)
CREATE OR ALTER PROCEDURE dbo.usp_Doctor_SearchBySymptomOrDepartment
    @SearchTerm NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET @SearchTerm = '%' + LTRIM(RTRIM(@SearchTerm)) + '%';

    SELECT TOP 10
        d.DoctorId,
        u.FullName AS DoctorName,
        d.Department,
        d.Specialty,
        d.ExperienceYears,
        d.ConsultationFeePerMinute,
        d.IsFreeConsultation,
        d.IsLive,
        u.AvatarUrl
    FROM dbo.Doctors d
    INNER JOIN dbo.Users u ON d.UserId = u.UserId
    WHERE d.IsApproved = 1 AND (
        d.Department LIKE @SearchTerm 
        OR d.Specialty LIKE @SearchTerm 
        OR u.FullName LIKE @SearchTerm
        OR d.Bio LIKE @SearchTerm
    )
    ORDER BY d.IsLive DESC, d.Rating DESC;
END
GO

-- 3. Toggle Follow Doctor
CREATE OR ALTER PROCEDURE dbo.usp_Doctor_ToggleFollow
    @DoctorId BIGINT,
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.DoctorFollowers WHERE DoctorId = @DoctorId AND UserId = @UserId)
    BEGIN
        DELETE FROM dbo.DoctorFollowers WHERE DoctorId = @DoctorId AND UserId = @UserId;
        UPDATE dbo.Doctors SET FollowerCount = CASE WHEN FollowerCount > 0 THEN FollowerCount - 1 ELSE 0 END WHERE DoctorId = @DoctorId;
        SELECT 0 AS IsFollowing, 'Unfollowed doctor' AS Message;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.DoctorFollowers (DoctorId, UserId, CreatedAt)
        VALUES (@DoctorId, @UserId, SYSUTCDATETIME());
        UPDATE dbo.Doctors SET FollowerCount = FollowerCount + 1 WHERE DoctorId = @DoctorId;
        SELECT 1 AS IsFollowing, 'Followed doctor' AS Message;
    END
END
GO

-- 4. Doctor Block / Remove User from Chat
CREATE OR ALTER PROCEDURE dbo.usp_Doctor_BlockUser
    @DoctorId BIGINT,
    @UserId BIGINT,
    @Reason NVARCHAR(255) = 'Abusive behavior'
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.DoctorBlockedUsers WHERE DoctorId = @DoctorId AND UserId = @UserId)
    BEGIN
        INSERT INTO dbo.DoctorBlockedUsers (DoctorId, UserId, Reason, CreatedAt)
        VALUES (@DoctorId, @UserId, @Reason, SYSUTCDATETIME());
    END

    SELECT 1 AS Success, 'User blocked successfully' AS Message;
END
GO

-- 4b. Unblock User from Doctor's Stream (Undo Block)
CREATE OR ALTER PROCEDURE dbo.usp_Doctor_UnblockUser
    @DoctorId BIGINT,
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DoctorBlockedUsers WHERE DoctorId = @DoctorId AND UserId = @UserId;
    SELECT 1 AS Success, 'User unblocked successfully' AS Message;
END
GO

-- 5. Check if User is Blocked by Doctor
CREATE OR ALTER PROCEDURE dbo.usp_Doctor_IsUserBlocked
    @DoctorId BIGINT,
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (
        SELECT 1 FROM dbo.DoctorBlockedUsers WHERE DoctorId = @DoctorId AND UserId = @UserId
    ) THEN 1 ELSE 0 END AS IsBlocked;
END
GO

-- 6. Add Live Stream Floating Comment
CREATE OR ALTER PROCEDURE dbo.usp_LiveStream_AddComment
    @StreamId BIGINT,
    @UserId BIGINT,
    @Message NVARCHAR(300)
AS
BEGIN
    SET NOCOUNT ON;
    -- Verify user is not blocked by doctor of this stream
    DECLARE @DoctorId BIGINT;
    SELECT @DoctorId = DoctorId FROM dbo.LiveStreams WHERE StreamId = @StreamId;

    IF EXISTS (SELECT 1 FROM dbo.DoctorBlockedUsers WHERE DoctorId = @DoctorId AND UserId = @UserId)
    BEGIN
        SELECT 0 AS Success, 'You are blocked by this doctor' AS Message, NULL AS CommentId;
        RETURN;
    END

    DECLARE @NewCommentId BIGINT;
    INSERT INTO dbo.LiveStreamComments (StreamId, UserId, Message, CreatedAt)
    VALUES (@StreamId, @UserId, @Message, SYSUTCDATETIME());

    SET @NewCommentId = SCOPE_IDENTITY();

    SELECT 
        1 AS Success, 
        'Comment added' AS Message, 
        @NewCommentId AS CommentId,
        u.UserId,
        u.FullName AS UserName,
        u.AvatarUrl
    FROM dbo.Users u WHERE u.UserId = @UserId;
END
GO

-- 7. Get Recent Floating Comments for Stream (Real-Time Overlay & Current Studio Session)
CREATE OR ALTER PROCEDURE dbo.usp_LiveStream_GetRecentComments
    @StreamId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 100
        c.CommentId,
        c.UserId,
        c.Message,
        c.CreatedAt,
        u.FullName AS UserName,
        u.AvatarUrl
    FROM dbo.LiveStreamComments c
    INNER JOIN dbo.Users u ON c.UserId = u.UserId
    WHERE c.StreamId = @StreamId
    ORDER BY c.CreatedAt DESC;
END
GO

-- 8. Toggle Like on Live Stream
CREATE OR ALTER PROCEDURE dbo.usp_LiveStream_ToggleLike
    @StreamId BIGINT,
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.LiveStreamLikes WHERE StreamId = @StreamId AND UserId = @UserId)
    BEGIN
        DELETE FROM dbo.LiveStreamLikes WHERE StreamId = @StreamId AND UserId = @UserId;
        UPDATE dbo.LiveStreams SET TotalLikes = CASE WHEN TotalLikes > 0 THEN TotalLikes - 1 ELSE 0 END WHERE StreamId = @StreamId;
        SELECT 0 AS HasLiked, 'Like removed' AS Message;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.LiveStreamLikes (StreamId, UserId, CreatedAt)
        VALUES (@StreamId, @UserId, SYSUTCDATETIME());
        UPDATE dbo.LiveStreams SET TotalLikes = TotalLikes + 1 WHERE StreamId = @StreamId;
        SELECT 1 AS HasLiked, 'Liked stream' AS Message;
    END
END
GO
