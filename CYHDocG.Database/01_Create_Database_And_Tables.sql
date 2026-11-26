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
    CREATE NONCLUSTERED INDEX IX_Doctors_Reels_Covering ON dbo.Doctors (IsApproved, IsLive DESC, ScheduledLiveAt ASC, Rating DESC) INCLUDE (DoctorId, UserId, Specialty, Department, Qualification, ExperienceYears, ConsultationFeePerMinute, IsFreeConsultation, LiveStreamUrl, FollowerCount);
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
    CREATE NONCLUSTERED INDEX IX_LiveStreams_Doctor_Active ON dbo.LiveStreams (DoctorId, IsActive) INCLUDE (StreamId, CurrentViewers, TotalLikes, Title, PlaybackUrl);
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
