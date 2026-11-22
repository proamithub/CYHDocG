-- ============================================================================
-- CYHDocGuru Database Schema: 01_Create_Database_And_Tables.sql
-- Database: CYHDocGuruDb (Microsoft SQL Server)
-- ============================================================================

-- Ensure we are using the live database CYHDOCGDB
IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. System & Admin Dynamic Settings
IF OBJECT_ID('dbo.AppSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings (
        SettingId INT IDENTITY(1,1) PRIMARY KEY,
        SettingKey NVARCHAR(100) NOT NULL UNIQUE,
        SettingValue NVARCHAR(MAX) NOT NULL,
        Description NVARCHAR(255) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 2. Master Roles
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles (
        RoleId INT IDENTITY(1,1) PRIMARY KEY,
        RoleName NVARCHAR(50) NOT NULL UNIQUE, -- 'Admin', 'Doctor', 'User'
        Description NVARCHAR(200) NULL
    );
END
GO

-- 3. Users Master Table (Supports Email + Google SSO)
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserId BIGINT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(256) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(500) NULL, -- Nullable for Google SSO only accounts
        Phone NVARCHAR(20) NULL,
        GoogleId NVARCHAR(256) NULL,
        RoleId INT NOT NULL CONSTRAINT FK_Users_Roles REFERENCES dbo.Roles(RoleId),
        AvatarUrl NVARCHAR(500) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL
    );
    CREATE NONCLUSTERED INDEX IX_Users_Email ON dbo.Users(Email);
    CREATE NONCLUSTERED INDEX IX_Users_GoogleId ON dbo.Users(GoogleId);
END
GO

-- 4. Doctor Profiles & Practice Details
IF OBJECT_ID('dbo.Doctors', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Doctors (
        DoctorId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL UNIQUE CONSTRAINT FK_Doctors_Users REFERENCES dbo.Users(UserId),
        Specialty NVARCHAR(150) NOT NULL,
        Department NVARCHAR(100) NOT NULL,
        Qualification NVARCHAR(200) NOT NULL,
        ExperienceYears INT NOT NULL DEFAULT 0,
        Bio NVARCHAR(MAX) NULL,
        ConsultationFeePerMinute DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        IsFreeConsultation BIT NOT NULL DEFAULT 0,
        IsLive BIT NOT NULL DEFAULT 0,
        LiveStreamUrl NVARCHAR(500) NULL,
        ScheduledLiveAt DATETIME2 NULL,
        FollowerCount INT NOT NULL DEFAULT 0,
        Rating DECIMAL(3,2) NOT NULL DEFAULT 5.00,
        IsApproved BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL
    );
    CREATE NONCLUSTERED INDEX IX_Doctors_Department ON dbo.Doctors(Department);
    CREATE NONCLUSTERED INDEX IX_Doctors_IsLive ON dbo.Doctors(IsLive);
END
GO

-- 5. Doctor Followers
IF OBJECT_ID('dbo.DoctorFollowers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DoctorFollowers (
        FollowerId BIGINT IDENTITY(1,1) PRIMARY KEY,
        DoctorId BIGINT NOT NULL CONSTRAINT FK_Followers_Doctor REFERENCES dbo.Doctors(DoctorId),
        UserId BIGINT NOT NULL CONSTRAINT FK_Followers_User REFERENCES dbo.Users(UserId),
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Doctor_User_Follow UNIQUE (DoctorId, UserId)
    );
END
GO

-- 6. Doctor Chat Moderation (Block / Remove Users)
IF OBJECT_ID('dbo.DoctorBlockedUsers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DoctorBlockedUsers (
        BlockId BIGINT IDENTITY(1,1) PRIMARY KEY,
        DoctorId BIGINT NOT NULL CONSTRAINT FK_Block_Doctor REFERENCES dbo.Doctors(DoctorId),
        UserId BIGINT NOT NULL CONSTRAINT FK_Block_User REFERENCES dbo.Users(UserId),
        Reason NVARCHAR(255) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Doctor_Block_User UNIQUE (DoctorId, UserId)
    );
END
GO

-- 7. Live Stream Sessions (For Reels Feed & Archival)
IF OBJECT_ID('dbo.LiveStreams', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LiveStreams (
        StreamId BIGINT IDENTITY(1,1) PRIMARY KEY,
        DoctorId BIGINT NOT NULL CONSTRAINT FK_LiveStreams_Doctor REFERENCES dbo.Doctors(DoctorId),
        Title NVARCHAR(200) NOT NULL,
        PlaybackUrl NVARCHAR(500) NOT NULL,
        ScheduledStartTime DATETIME2 NULL,
        StartedAt DATETIME2 NULL,
        EndedAt DATETIME2 NULL,
        IsActive BIT NOT NULL DEFAULT 0,
        CurrentViewers INT NOT NULL DEFAULT 0,
        TotalLikes INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 8. Live Stream Floating Comments (Latest 5 streamed real-time)
IF OBJECT_ID('dbo.LiveStreamComments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LiveStreamComments (
        CommentId BIGINT IDENTITY(1,1) PRIMARY KEY,
        StreamId BIGINT NOT NULL CONSTRAINT FK_Comments_Stream REFERENCES dbo.LiveStreams(StreamId),
        UserId BIGINT NOT NULL CONSTRAINT FK_Comments_User REFERENCES dbo.Users(UserId),
        Message NVARCHAR(300) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE NONCLUSTERED INDEX IX_Comments_Stream_Created ON dbo.LiveStreamComments(StreamId, CreatedAt DESC);
END
GO

-- 9. Live Stream Likes
IF OBJECT_ID('dbo.LiveStreamLikes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LiveStreamLikes (
        LikeId BIGINT IDENTITY(1,1) PRIMARY KEY,
        StreamId BIGINT NOT NULL CONSTRAINT FK_Likes_Stream REFERENCES dbo.LiveStreams(StreamId),
        UserId BIGINT NOT NULL CONSTRAINT FK_Likes_User REFERENCES dbo.Users(UserId),
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Stream_User_Like UNIQUE (StreamId, UserId)
    );
END
GO

-- 10. User Wallets
IF OBJECT_ID('dbo.UserWallets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserWallets (
        WalletId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL UNIQUE CONSTRAINT FK_Wallet_User REFERENCES dbo.Users(UserId),
        Balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        Currency NVARCHAR(10) NOT NULL DEFAULT 'INR',
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 11. Doctor Wallets / Earnings Account
IF OBJECT_ID('dbo.DoctorWallets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DoctorWallets (
        DoctorWalletId BIGINT IDENTITY(1,1) PRIMARY KEY,
        DoctorId BIGINT NOT NULL UNIQUE CONSTRAINT FK_DoctorWallet_Doctor REFERENCES dbo.Doctors(DoctorId),
        Balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        TotalWithdrawn DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 12. 1-to-1 Consultations (Synchronized Timer & Billing)
IF OBJECT_ID('dbo.Consultations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Consultations (
        ConsultationId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL CONSTRAINT FK_Consultation_User REFERENCES dbo.Users(UserId),
        DoctorId BIGINT NOT NULL CONSTRAINT FK_Consultation_Doctor REFERENCES dbo.Doctors(DoctorId),
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Active, Completed, Cancelled
        StartedAt DATETIME2 NULL,
        EndedAt DATETIME2 NULL,
        DurationSeconds INT NOT NULL DEFAULT 0,
        RatePerMinute DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        IsPaid BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 13. Consultation 1-to-1 Chat History
IF OBJECT_ID('dbo.ConsultationMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConsultationMessages (
        MessageId BIGINT IDENTITY(1,1) PRIMARY KEY,
        ConsultationId BIGINT NOT NULL CONSTRAINT FK_ConsultationMsg_Consultation REFERENCES dbo.Consultations(ConsultationId),
        SenderId BIGINT NOT NULL CONSTRAINT FK_ConsultationMsg_Sender REFERENCES dbo.Users(UserId),
        SenderRole NVARCHAR(20) NOT NULL, -- 'User', 'Doctor', 'AI'
        MessageText NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE NONCLUSTERED INDEX IX_ConsultationMsg_ConsultationId ON dbo.ConsultationMessages(ConsultationId, CreatedAt ASC);
END
GO

-- 14. Financial Ledger (Unified multi-party audit)
IF OBJECT_ID('dbo.PlatformLedger', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformLedger (
        LedgerId BIGINT IDENTITY(1,1) PRIMARY KEY,
        TransactionType NVARCHAR(50) NOT NULL, -- 'WalletTopup', 'ConsultationDebit', 'DoctorCredit', 'PlatformCut'
        FromUserId BIGINT NULL CONSTRAINT FK_Ledger_FromUser REFERENCES dbo.Users(UserId),
        ToDoctorId BIGINT NULL CONSTRAINT FK_Ledger_ToDoctor REFERENCES dbo.Doctors(DoctorId),
        Amount DECIMAL(18,2) NOT NULL,
        PlatformFee DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        ReferenceId NVARCHAR(100) NULL, -- ConsultationId or PaymentGatewayTxnId
        Description NVARCHAR(255) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 15. User Wallet Transaction History
IF OBJECT_ID('dbo.WalletTransactions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WalletTransactions (
        TransactionId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL CONSTRAINT FK_WalletTx_User REFERENCES dbo.Users(UserId),
        Type NVARCHAR(20) NOT NULL, -- 'Credit' (Top-Up), 'Debit' (Consultation)
        Amount DECIMAL(18,2) NOT NULL,
        BalanceAfter DECIMAL(18,2) NOT NULL,
        ReferenceType NVARCHAR(50) NULL, -- 'Razorpay', 'Consultation'
        ReferenceId NVARCHAR(100) NULL,
        Description NVARCHAR(255) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 16. Master Medicines (For fast 3-4 letter typing auto-suggest)
IF OBJECT_ID('dbo.MasterMedicines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MasterMedicines (
        MasterMedicineId BIGINT IDENTITY(1,1) PRIMARY KEY,
        BrandName NVARCHAR(200) NOT NULL,
        GenericName NVARCHAR(200) NULL,
        DosageForm NVARCHAR(50) NOT NULL DEFAULT 'Tablet', -- Tablet, Syrup, Injection, Capsule
        Strength NVARCHAR(50) NULL,
        Manufacturer NVARCHAR(150) NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
    CREATE NONCLUSTERED INDEX IX_MasterMedicines_BrandName ON dbo.MasterMedicines(BrandName);
END
GO

-- 17. Prescriptions Master (With Compressed File & OCR Text)
IF OBJECT_ID('dbo.Prescriptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Prescriptions (
        PrescriptionId BIGINT IDENTITY(1,1) PRIMARY KEY,
        ConsultationId BIGINT NULL CONSTRAINT FK_Prescriptions_Consultation REFERENCES dbo.Consultations(ConsultationId),
        DoctorId BIGINT NOT NULL CONSTRAINT FK_Prescriptions_Doctor REFERENCES dbo.Doctors(DoctorId),
        UserId BIGINT NOT NULL CONSTRAINT FK_Prescriptions_User REFERENCES dbo.Users(UserId),
        OriginalFileUrl NVARCHAR(500) NULL,
        CompressedFileUrl NVARCHAR(500) NULL,
        OcrExtractedText NVARCHAR(MAX) NULL,
        DoctorNotes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 18. Prescription Medicines (Items in Rx with dosage)
IF OBJECT_ID('dbo.PrescriptionMedicines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrescriptionMedicines (
        PrescriptionMedicineId BIGINT IDENTITY(1,1) PRIMARY KEY,
        PrescriptionId BIGINT NOT NULL CONSTRAINT FK_RxMedicines_Prescription REFERENCES dbo.Prescriptions(PrescriptionId),
        MedicineName NVARCHAR(200) NOT NULL,
        DosageSchedule NVARCHAR(50) NOT NULL, -- e.g. '1-0-1' (Morning - Afternoon - Night)
        TimingInstruction NVARCHAR(100) NULL, -- 'After Meal', 'Before Meal'
        DurationDays INT NOT NULL DEFAULT 1,
        TotalUnits INT NOT NULL DEFAULT 1,
        CourseEndDate DATE NOT NULL,
        RefillAlertDate DATE NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 19. Medicine Reminders & Refill Alerts
IF OBJECT_ID('dbo.MedicineReminders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MedicineReminders (
        ReminderId BIGINT IDENTITY(1,1) PRIMARY KEY,
        PrescriptionMedicineId BIGINT NOT NULL CONSTRAINT FK_Reminders_RxMed REFERENCES dbo.PrescriptionMedicines(PrescriptionMedicineId),
        UserId BIGINT NOT NULL CONSTRAINT FK_Reminders_User REFERENCES dbo.Users(UserId),
        ReminderDateTime DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Taken', 'Missed', 'RefillAlert'
        NotifiedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE NONCLUSTERED INDEX IX_Reminders_User_DateTime ON dbo.MedicineReminders(UserId, ReminderDateTime);
END
GO

-- 20. Security & Anti-Inspect Audit Logs
IF OBJECT_ID('dbo.SecurityLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SecurityLogs (
        LogId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NULL CONSTRAINT FK_SecurityLogs_User REFERENCES dbo.Users(UserId),
        EventAction NVARCHAR(100) NOT NULL, -- 'DevToolsAttempt', 'InspectKeyShortcut', 'RightClickBlocked'
        IpAddress NVARCHAR(50) NULL,
        UserAgent NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO
-- ============================================================================
-- CYHDocGuru Stored Procedures: 02_Stored_Procedures_Auth_And_Security.sql
-- ============================================================================

IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. AppSettings Get All or By Key
CREATE OR ALTER PROCEDURE dbo.usp_AppSettings_Get
    @SettingKey NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @SettingKey IS NULL
    BEGIN
        SELECT SettingId, SettingKey, SettingValue, Description, IsActive, UpdatedAt
        FROM dbo.AppSettings;
    END
    ELSE
    BEGIN
        SELECT SettingId, SettingKey, SettingValue, Description, IsActive, UpdatedAt
        FROM dbo.AppSettings
        WHERE SettingKey = @SettingKey;
    END
END
GO

-- 2. AppSettings Update (e.g. Admin toggling Anti-Inspect on/off)
CREATE OR ALTER PROCEDURE dbo.usp_AppSettings_Update
    @SettingKey NVARCHAR(100),
    @SettingValue NVARCHAR(MAX),
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = @SettingKey)
    BEGIN
        UPDATE dbo.AppSettings
        SET SettingValue = @SettingValue,
            IsActive = @IsActive,
            UpdatedAt = SYSUTCDATETIME()
        WHERE SettingKey = @SettingKey;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.AppSettings (SettingKey, SettingValue, IsActive, UpdatedAt)
        VALUES (@SettingKey, @SettingValue, @IsActive, SYSUTCDATETIME());
    END

    SELECT 1 AS Success, 'Setting updated successfully' AS Message;
END
GO

-- 3. Register Standard User
CREATE OR ALTER PROCEDURE dbo.usp_Auth_RegisterUser
    @FullName NVARCHAR(150),
    @Email NVARCHAR(256),
    @PasswordHash NVARCHAR(500),
    @Phone NVARCHAR(20) = NULL,
    @RoleName NVARCHAR(50) = 'User'
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email)
        BEGIN
            SELECT 0 AS Success, 'Email already registered' AS Message, NULL AS UserId;
            RETURN;
        END

        DECLARE @RoleId INT;
        SELECT @RoleId = RoleId FROM dbo.Roles WHERE RoleName = @RoleName;
        IF @RoleId IS NULL
        BEGIN
            SELECT @RoleId = RoleId FROM dbo.Roles WHERE RoleName = 'User';
        END

        DECLARE @NewUserId BIGINT;
        INSERT INTO dbo.Users (FullName, Email, PasswordHash, Phone, RoleId, IsActive, CreatedAt)
        VALUES (@FullName, @Email, @PasswordHash, @Phone, @RoleId, 1, SYSUTCDATETIME());

        SET @NewUserId = SCOPE_IDENTITY();

        -- Initialize User Wallet with 0 balance
        INSERT INTO dbo.UserWallets (UserId, Balance, Currency, UpdatedAt)
        VALUES (@NewUserId, 0.00, 'INR', SYSUTCDATETIME());

        SELECT 1 AS Success, 'User registered successfully' AS Message, @NewUserId AS UserId;
    END TRY
    BEGIN CATCH
        SELECT 0 AS Success, ERROR_MESSAGE() AS Message, NULL AS UserId;
    END CATCH
END
GO

-- 4. User Login (Email / Password)
CREATE OR ALTER PROCEDURE dbo.usp_Auth_LoginUser
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        u.UserId,
        u.FullName,
        u.Email,
        u.PasswordHash,
        u.Phone,
        u.AvatarUrl,
        u.IsActive,
        r.RoleName,
        w.Balance AS WalletBalance,
        d.DoctorId
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.UserWallets w ON u.UserId = w.UserId
    LEFT JOIN dbo.Doctors d ON u.UserId = d.UserId
    WHERE u.Email = @Email AND u.IsActive = 1;
END
GO

-- 5. Google SSO Login or Register
CREATE OR ALTER PROCEDURE dbo.usp_Auth_GoogleLogin
    @GoogleId NVARCHAR(256),
    @Email NVARCHAR(256),
    @FullName NVARCHAR(150),
    @AvatarUrl NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId BIGINT;
    DECLARE @RoleId INT;

    SELECT @UserId = UserId FROM dbo.Users WHERE GoogleId = @GoogleId OR Email = @Email;

    IF @UserId IS NULL
    BEGIN
        SELECT @RoleId = RoleId FROM dbo.Roles WHERE RoleName = 'User';

        INSERT INTO dbo.Users (FullName, Email, GoogleId, RoleId, AvatarUrl, IsActive, CreatedAt)
        VALUES (@FullName, @Email, @GoogleId, @RoleId, @AvatarUrl, 1, SYSUTCDATETIME());

        SET @UserId = SCOPE_IDENTITY();

        -- Initialize wallet
        INSERT INTO dbo.UserWallets (UserId, Balance, Currency, UpdatedAt)
        VALUES (@UserId, 0.00, 'INR', SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        -- Update avatar and google id if missing
        UPDATE dbo.Users
        SET GoogleId = ISNULL(GoogleId, @GoogleId),
            AvatarUrl = ISNULL(AvatarUrl, @AvatarUrl),
            UpdatedAt = SYSUTCDATETIME()
        WHERE UserId = @UserId;
    END

    -- Return full user profile
    SELECT 
        u.UserId,
        u.FullName,
        u.Email,
        u.Phone,
        u.AvatarUrl,
        r.RoleName,
        ISNULL(w.Balance, 0.00) AS WalletBalance,
        d.DoctorId
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.UserWallets w ON u.UserId = w.UserId
    LEFT JOIN dbo.Doctors d ON u.UserId = d.UserId
    WHERE u.UserId = @UserId;
END
GO

-- 6. Log Security Event (DevTools or Inspect attempts)
CREATE OR ALTER PROCEDURE dbo.usp_Security_LogEvent
    @UserId BIGINT = NULL,
    @EventAction NVARCHAR(100),
    @IpAddress NVARCHAR(50) = NULL,
    @UserAgent NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.SecurityLogs (UserId, EventAction, IpAddress, UserAgent, CreatedAt)
    VALUES (@UserId, @EventAction, @IpAddress, @UserAgent, SYSUTCDATETIME());

    SELECT 1 AS Success;
END
GO

-- 7. Change Password
CREATE OR ALTER PROCEDURE dbo.usp_User_ChangePassword
    @UserId BIGINT,
    @OldPasswordHash NVARCHAR(500),
    @NewPasswordHash NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @UserId AND PasswordHash = @OldPasswordHash)
    BEGIN
        UPDATE dbo.Users
        SET PasswordHash = @NewPasswordHash,
            UpdatedAt = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        SELECT 1 AS Success, 'Password changed successfully' AS Message;
    END
    ELSE
    BEGIN
        SELECT 0 AS Success, 'Current password is incorrect' AS Message;
    END
END
GO
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
    WHERE d.IsApproved = 1 AND (d.IsLive = 1 OR d.ScheduledLiveAt IS NOT NULL)
    ORDER BY d.IsLive DESC, d.ScheduledLiveAt ASC
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
-- ============================================================================
-- CYHDocGuru Stored Procedures: 04_Stored_Procedures_Consultation_And_Wallet.sql
-- ============================================================================

IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. Get User Wallet Balance
CREATE OR ALTER PROCEDURE dbo.usp_Wallet_GetBalance
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        w.WalletId,
        w.UserId,
        w.Balance,
        w.Currency,
        w.UpdatedAt
    FROM dbo.UserWallets w
    WHERE w.UserId = @UserId;
END
GO

-- 2. User Wallet Top-Up (Add Money)
CREATE OR ALTER PROCEDURE dbo.usp_Wallet_TopUp
    @UserId BIGINT,
    @Amount DECIMAL(18,2),
    @ReferenceId NVARCHAR(100), -- Gateway Payment ID
    @Description NVARCHAR(255) = 'Wallet Top-Up'
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        IF @Amount <= 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 0 AS Success, 'Amount must be greater than zero' AS Message, 0.00 AS NewBalance;
            RETURN;
        END

        UPDATE dbo.UserWallets
        SET Balance = Balance + @Amount,
            UpdatedAt = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        DECLARE @NewBalance DECIMAL(18,2);
        SELECT @NewBalance = Balance FROM dbo.UserWallets WHERE UserId = @UserId;

        -- Record in WalletTransactions
        INSERT INTO dbo.WalletTransactions (UserId, Type, Amount, BalanceAfter, ReferenceType, ReferenceId, Description, CreatedAt)
        VALUES (@UserId, 'Credit', @Amount, @NewBalance, 'TopUp', @ReferenceId, @Description, SYSUTCDATETIME());

        -- Record in PlatformLedger
        INSERT INTO dbo.PlatformLedger (TransactionType, FromUserId, ToDoctorId, Amount, PlatformFee, ReferenceId, Description, CreatedAt)
        VALUES ('WalletTopup', @UserId, NULL, @Amount, 0.00, @ReferenceId, @Description, SYSUTCDATETIME());

        COMMIT TRANSACTION;
        SELECT 1 AS Success, 'Wallet topped up successfully' AS Message, @NewBalance AS NewBalance;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SELECT 0 AS Success, ERROR_MESSAGE() AS Message, 0.00 AS NewBalance;
    END CATCH
END
GO

-- 3. Get User Wallet Transaction History
CREATE OR ALTER PROCEDURE dbo.usp_Wallet_GetTransactionHistory
    @UserId BIGINT,
    @PageNumber INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT 
        TransactionId,
        Type,
        Amount,
        BalanceAfter,
        ReferenceType,
        ReferenceId,
        Description,
        CreatedAt
    FROM dbo.WalletTransactions
    WHERE UserId = @UserId
    ORDER BY CreatedAt DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- 4. Create Consultation Request (From AI Escalation or Direct Request)
CREATE OR ALTER PROCEDURE dbo.usp_Consultation_CreateRequest
    @UserId BIGINT,
    @DoctorId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        DECLARE @IsFree BIT;
        DECLARE @Rate DECIMAL(18,2);
        DECLARE @UserBalance DECIMAL(18,2);

        SELECT @IsFree = IsFreeConsultation, @Rate = ConsultationFeePerMinute 
        FROM dbo.Doctors WHERE DoctorId = @DoctorId;

        SELECT @UserBalance = Balance FROM dbo.UserWallets WHERE UserId = @UserId;

        -- If paid, check if user has at least minimum balance for 1 minute
        IF @IsFree = 0 AND (@UserBalance IS NULL OR @UserBalance < @Rate)
        BEGIN
            SELECT 0 AS Success, 'Insufficient wallet balance for paid consultation' AS Message, NULL AS ConsultationId;
            RETURN;
        END

        DECLARE @NewConsultationId BIGINT;
        INSERT INTO dbo.Consultations (UserId, DoctorId, Status, RatePerMinute, IsPaid, CreatedAt)
        VALUES (@UserId, @DoctorId, 'Pending', CASE WHEN @IsFree = 1 THEN 0.00 ELSE @Rate END, CASE WHEN @IsFree = 1 THEN 0 ELSE 1 END, SYSUTCDATETIME());

        SET @NewConsultationId = SCOPE_IDENTITY();

        SELECT 1 AS Success, 'Consultation request created' AS Message, @NewConsultationId AS ConsultationId;
    END TRY
    BEGIN CATCH
        SELECT 0 AS Success, ERROR_MESSAGE() AS Message, NULL AS ConsultationId;
    END CATCH
END
GO

-- 5. Doctor Accepts Consultation (Starts Timer)
CREATE OR ALTER PROCEDURE dbo.usp_Consultation_Accept
    @ConsultationId BIGINT,
    @DoctorId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Consultations WHERE ConsultationId = @ConsultationId AND DoctorId = @DoctorId AND Status = 'Pending')
    BEGIN
        UPDATE dbo.Consultations
        SET Status = 'Active',
            StartedAt = SYSUTCDATETIME()
        WHERE ConsultationId = @ConsultationId;

        SELECT 1 AS Success, 'Consultation started' AS Message, SYSUTCDATETIME() AS StartedAt;
    END
    ELSE
    BEGIN
        SELECT 0 AS Success, 'Invalid or already processed consultation' AS Message, NULL AS StartedAt;
    END
END
GO

-- 6. End Consultation & Synchronized Timer Settlement (Wallet Deduction & Ledger)
CREATE OR ALTER PROCEDURE dbo.usp_Consultation_EndAndDeductWallet
    @ConsultationId BIGINT,
    @EndedByUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @UserId BIGINT, @DoctorId BIGINT, @StartedAt DATETIME2, @Rate DECIMAL(18,2), @IsPaid BIT;
        SELECT 
            @UserId = UserId, 
            @DoctorId = DoctorId, 
            @StartedAt = StartedAt, 
            @Rate = RatePerMinute,
            @IsPaid = IsPaid
        FROM dbo.Consultations
        WHERE ConsultationId = @ConsultationId AND Status = 'Active';

        IF @StartedAt IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 0 AS Success, 'Active consultation not found' AS Message;
            RETURN;
        END

        DECLARE @EndedAt DATETIME2 = SYSUTCDATETIME();
        DECLARE @DurationSeconds INT = DATEDIFF(SECOND, @StartedAt, @EndedAt);
        DECLARE @BilledMinutes DECIMAL(18,2) = CEILING(CAST(@DurationSeconds AS FLOAT) / 60.0);
        IF @BilledMinutes < 1 SET @BilledMinutes = 1;

        DECLARE @TotalBill DECIMAL(18,2) = 0.00;
        IF @IsPaid = 1
        BEGIN
            SET @TotalBill = @BilledMinutes * @Rate;
        END

        -- Deduct from user wallet if paid
        IF @TotalBill > 0
        BEGIN
            DECLARE @UserBalance DECIMAL(18,2);
            SELECT @UserBalance = Balance FROM dbo.UserWallets WHERE UserId = @UserId;

            -- Deduct maximum possible up to balance
            IF @TotalBill > @UserBalance SET @TotalBill = @UserBalance;

            UPDATE dbo.UserWallets 
            SET Balance = Balance - @TotalBill, UpdatedAt = @EndedAt 
            WHERE UserId = @UserId;

            -- Calculate 80% to Doctor, 20% platform commission
            DECLARE @DoctorCut DECIMAL(18,2) = @TotalBill * 0.80;
            DECLARE @PlatformCut DECIMAL(18,2) = @TotalBill * 0.20;

            -- Credit Doctor Wallet
            IF NOT EXISTS (SELECT 1 FROM dbo.DoctorWallets WHERE DoctorId = @DoctorId)
            BEGIN
                INSERT INTO dbo.DoctorWallets (DoctorId, Balance, TotalWithdrawn, UpdatedAt)
                VALUES (@DoctorId, @DoctorCut, 0.00, @EndedAt);
            END
            ELSE
            BEGIN
                UPDATE dbo.DoctorWallets
                SET Balance = Balance + @DoctorCut, UpdatedAt = @EndedAt
                WHERE DoctorId = @DoctorId;
            END

            -- Record User Wallet Tx
            INSERT INTO dbo.WalletTransactions (UserId, Type, Amount, BalanceAfter, ReferenceType, ReferenceId, Description, CreatedAt)
            VALUES (@UserId, 'Debit', @TotalBill, (@UserBalance - @TotalBill), 'Consultation', CAST(@ConsultationId AS NVARCHAR), '1-to-1 Doctor Consultation', @EndedAt);

            -- Record in PlatformLedger
            INSERT INTO dbo.PlatformLedger (TransactionType, FromUserId, ToDoctorId, Amount, PlatformFee, ReferenceId, Description, CreatedAt)
            VALUES ('ConsultationDebit', @UserId, @DoctorId, @TotalBill, @PlatformCut, CAST(@ConsultationId AS NVARCHAR), 'Consultation fee settlement', @EndedAt);
        END

        -- Mark consultation completed
        UPDATE dbo.Consultations
        SET Status = 'Completed',
            EndedAt = @EndedAt,
            DurationSeconds = @DurationSeconds,
            TotalAmount = @TotalBill
        WHERE ConsultationId = @ConsultationId;

        COMMIT TRANSACTION;
        SELECT 1 AS Success, 'Consultation settled' AS Message, @DurationSeconds AS DurationSeconds, @TotalBill AS BilledAmount;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SELECT 0 AS Success, ERROR_MESSAGE() AS Message, 0 AS DurationSeconds, 0.00 AS BilledAmount;
    END CATCH
END
GO
-- ============================================================================
-- CYHDocGuru Stored Procedures: 05_Stored_Procedures_Prescription_And_Reminders.sql
-- ============================================================================

IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. Fast Master Medicine Search (Debounced 3-4 letters autocomplete)
CREATE OR ALTER PROCEDURE dbo.usp_MasterMedicine_Search
    @Query NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET @Query = LTRIM(RTRIM(@Query)) + '%'; -- Prefix search for indexed speed

    SELECT TOP 15
        MasterMedicineId,
        BrandName,
        GenericName,
        DosageForm,
        Strength,
        Manufacturer
    FROM dbo.MasterMedicines
    WHERE IsActive = 1 AND (BrandName LIKE @Query OR GenericName LIKE @Query)
    ORDER BY BrandName ASC;
END
GO

-- 2. Create Prescription Master
CREATE OR ALTER PROCEDURE dbo.usp_Prescription_Create
    @ConsultationId BIGINT = NULL,
    @DoctorId BIGINT,
    @UserId BIGINT,
    @OriginalFileUrl NVARCHAR(500) = NULL,
    @CompressedFileUrl NVARCHAR(500) = NULL,
    @OcrExtractedText NVARCHAR(MAX) = NULL,
    @DoctorNotes NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @NewPrescriptionId BIGINT;

    INSERT INTO dbo.Prescriptions (
        ConsultationId,
        DoctorId,
        UserId,
        OriginalFileUrl,
        CompressedFileUrl,
        OcrExtractedText,
        DoctorNotes,
        CreatedAt
    )
    VALUES (
        @ConsultationId,
        @DoctorId,
        @UserId,
        @OriginalFileUrl,
        @CompressedFileUrl,
        @OcrExtractedText,
        @DoctorNotes,
        SYSUTCDATETIME()
    );

    SET @NewPrescriptionId = SCOPE_IDENTITY();
    SELECT 1 AS Success, 'Prescription saved' AS Message, @NewPrescriptionId AS PrescriptionId;
END
GO

-- 3. Add Medicine Item to Prescription & Auto-Generate Reminders
CREATE OR ALTER PROCEDURE dbo.usp_Prescription_AddMedicine
    @PrescriptionId BIGINT,
    @MedicineName NVARCHAR(200),
    @DosageSchedule NVARCHAR(50), -- e.g. '1-0-1'
    @TimingInstruction NVARCHAR(100), -- 'After Meal'
    @DurationDays INT,
    @TotalUnits INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId BIGINT;
    SELECT @UserId = UserId FROM dbo.Prescriptions WHERE PrescriptionId = @PrescriptionId;

    DECLARE @StartDate DATE = CAST(SYSUTCDATETIME() AS DATE);
    DECLARE @CourseEndDate DATE = DATEADD(DAY, @DurationDays, @StartDate);
    DECLARE @RefillAlertDate DATE = DATEADD(DAY, -2, @CourseEndDate); -- Alert 2 days before course ends

    DECLARE @NewMedicineId BIGINT;
    INSERT INTO dbo.PrescriptionMedicines (
        PrescriptionId,
        MedicineName,
        DosageSchedule,
        TimingInstruction,
        DurationDays,
        TotalUnits,
        CourseEndDate,
        RefillAlertDate,
        CreatedAt
    )
    VALUES (
        @PrescriptionId,
        @MedicineName,
        @DosageSchedule,
        @TimingInstruction,
        @DurationDays,
        @TotalUnits,
        @CourseEndDate,
        @RefillAlertDate,
        SYSUTCDATETIME()
    );

    SET @NewMedicineId = SCOPE_IDENTITY();

    -- Generate Schedule Reminders for today/upcoming
    -- Morning (08:00 AM)
    IF @DosageSchedule LIKE '1-%'
    BEGIN
        INSERT INTO dbo.MedicineReminders (PrescriptionMedicineId, UserId, ReminderDateTime, Status)
        VALUES (@NewMedicineId, @UserId, DATEADD(HOUR, 8, CAST(@StartDate AS DATETIME2)), 'Pending');
    END
    -- Afternoon (01:00 PM)
    IF @DosageSchedule LIKE '%-1-%'
    BEGIN
        INSERT INTO dbo.MedicineReminders (PrescriptionMedicineId, UserId, ReminderDateTime, Status)
        VALUES (@NewMedicineId, @UserId, DATEADD(HOUR, 13, CAST(@StartDate AS DATETIME2)), 'Pending');
    END
    -- Night (08:30 PM)
    IF @DosageSchedule LIKE '%-1'
    BEGIN
        INSERT INTO dbo.MedicineReminders (PrescriptionMedicineId, UserId, ReminderDateTime, Status)
        VALUES (@NewMedicineId, @UserId, DATEADD(MINUTE, 510, DATEADD(HOUR, 12, CAST(@StartDate AS DATETIME2))), 'Pending');
    END

    -- Refill Reminder Alert
    INSERT INTO dbo.MedicineReminders (PrescriptionMedicineId, UserId, ReminderDateTime, Status)
    VALUES (@NewMedicineId, @UserId, CAST(@RefillAlertDate AS DATETIME2), 'RefillAlert');

    SELECT 1 AS Success, 'Medicine added with reminders' AS Message, @NewMedicineId AS PrescriptionMedicineId;
END
GO

-- 4. Get User Prescriptions & Medicine Reminders
CREATE OR ALTER PROCEDURE dbo.usp_Prescription_GetByUserId
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        p.PrescriptionId,
        p.CreatedAt,
        p.CompressedFileUrl,
        p.DoctorNotes,
        d.Specialty,
        u.FullName AS DoctorName
    FROM dbo.Prescriptions p
    INNER JOIN dbo.Doctors d ON p.DoctorId = d.DoctorId
    INNER JOIN dbo.Users u ON d.UserId = u.UserId
    WHERE p.UserId = @UserId
    ORDER BY p.CreatedAt DESC;

    SELECT 
        pm.PrescriptionMedicineId,
        pm.PrescriptionId,
        pm.MedicineName,
        pm.DosageSchedule,
        pm.TimingInstruction,
        pm.DurationDays,
        pm.CourseEndDate,
        pm.RefillAlertDate
    FROM dbo.PrescriptionMedicines pm
    INNER JOIN dbo.Prescriptions p ON pm.PrescriptionId = p.PrescriptionId
    WHERE p.UserId = @UserId
    ORDER BY pm.CourseEndDate DESC;
END
GO
-- ============================================================================
-- CYHDocGuru Seed Data: 06_Seed_Data.sql
-- ============================================================================

IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. Insert Default Roles
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'Admin')
    INSERT INTO dbo.Roles (RoleName, Description) VALUES ('Admin', 'System Administrator with full access');

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'Doctor')
    INSERT INTO dbo.Roles (RoleName, Description) VALUES ('Doctor', 'Medical Practitioner with live stream & consultation rights');

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'User')
    INSERT INTO dbo.Roles (RoleName, Description) VALUES ('User', 'Patient / Portal User');
GO

-- 2. Insert Default App Settings (Anti-Inspect, Guest Scrolls, Platform Fee)
IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'AntiInspectEnabled')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('AntiInspectEnabled', '1', 'Toggles DevTools & Right-Click blocking with security toast alert', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'MaxGuestReelScrolls')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('MaxGuestReelScrolls', '5', 'Maximum number of reels a non-logged-in guest can scroll before mandatory login', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'PlatformFeePercent')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('PlatformFeePercent', '20', 'Platform commission percentage on doctor paid consultations', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'DefaultLanguage')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('DefaultLanguage', 'English', 'Default UI language for public portal', 1);
ELSE
    UPDATE dbo.AppSettings SET SettingValue = 'English' WHERE SettingKey = 'DefaultLanguage';

-- AI & Auth Dynamic Configurations
IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'GoogleClientId')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('GoogleClientId', 'YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com', 'Google OAuth 2.0 Client ID for One-Tap SSO', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'GoogleClientSecret')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('GoogleClientSecret', 'YOUR_GOOGLE_CLIENT_SECRET', 'Google OAuth 2.0 Client Secret', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'AiProvider')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('AiProvider', 'GoogleGemini', 'Primary LLM AI Provider (GoogleGemini / OpenAI)', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'AiModel')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('AiModel', 'gemini-1.5-flash', 'AI Model name for Clinical Triage & OCR', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'AiApiKey')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('AiApiKey', 'YOUR_GEMINI_API_KEY', 'API Key for Gemini / OpenAI LLM integration', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'AiEnabled')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('AiEnabled', '1', 'Master switch to enable external LLM clinical triage (1=Enabled, 0=Rule-engine only)', 1);
GO

-- 3. Insert Master Medicines (For fast 3-4 letter autocomplete test)
IF NOT EXISTS (SELECT 1 FROM dbo.MasterMedicines WHERE BrandName = 'Paracetamol 650')
BEGIN
    INSERT INTO dbo.MasterMedicines (BrandName, GenericName, DosageForm, Strength, Manufacturer) VALUES
    ('Paracetamol 650', 'Acetaminophen', 'Tablet', '650mg', 'Apex Labs'),
    ('Pantop 40', 'Pantoprazole', 'Tablet', '40mg', 'Aristo Pharma'),
    ('Pan-D', 'Pantoprazole + Domperidone', 'Capsule', '40mg/30mg', 'Alkem Labs'),
    ('Azithral 500', 'Azithromycin', 'Tablet', '500mg', 'Alembic Pharma'),
    ('Augmentin 625 Duo', 'Amoxicillin + Clavulanic Acid', 'Tablet', '625mg', 'GSK'),
    ('Cetzine', 'Cetirizine', 'Tablet', '10mg', 'Dr. Reddy'),
    ('Montair-LC', 'Montelukast + Levocetirizine', 'Tablet', '10mg/5mg', 'Cipla'),
    ('Glycomet 500', 'Metformin', 'Tablet', '500mg', 'USV Pvt Ltd'),
    ('Telma 40', 'Telmisartan', 'Tablet', '40mg', 'Glenmark'),
    ('Combiflam', 'Ibuprofen + Paracetamol', 'Tablet', '400mg/325mg', 'Sanofi'),
    ('Allegra 120', 'Fexofenadine', 'Tablet', '120mg', 'Sanofi'),
    ('Dolo 650', 'Paracetamol', 'Tablet', '650mg', 'Micro Labs'),
    ('Ascoril D Plus', 'Dextromethorphan + Phenylephrine', 'Syrup', '100ml', 'Glenmark');
END
GO
