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
