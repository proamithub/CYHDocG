-- ============================================================================
-- CYHDocGuru: 09_Doctor_Verification_And_Unique_Phone.sql
-- 1. Enforces Unique Phone Index on dbo.Users
-- 2. Updates usp_Auth_RegisterUser to require Phone, check unique Phone & Email,
--    and create Doctor profile with IsApproved = 0 (Pending Admin Approval)
-- 3. Stored Procedure for Admin to view and toggle Users active status
-- ============================================================================

IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. Create Unique Filtered Index on Users(Phone)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_Phone' AND object_id = OBJECT_ID('dbo.Users'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Users_Phone 
    ON dbo.Users(Phone) 
    WHERE Phone IS NOT NULL AND Phone <> '';
END
GO

-- 2. Update Registration Stored Procedure
CREATE OR ALTER PROCEDURE dbo.usp_Auth_RegisterUser
    @FullName NVARCHAR(150),
    @Email NVARCHAR(256),
    @PasswordHash NVARCHAR(500),
    @Phone NVARCHAR(20),
    @RoleName NVARCHAR(50) = 'User'
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        -- Validate Mandatory Fields
        IF @Phone IS NULL OR LTRIM(RTRIM(@Phone)) = ''
        BEGIN
            SELECT 0 AS Success, 'Mobile number is required' AS Message, NULL AS UserId;
            RETURN;
        END

        IF @Email IS NULL OR LTRIM(RTRIM(@Email)) = ''
        BEGIN
            SELECT 0 AS Success, 'Email address is required' AS Message, NULL AS UserId;
            RETURN;
        END

        SET @Phone = LTRIM(RTRIM(@Phone));
        SET @Email = LTRIM(RTRIM(@Email));

        -- Check Unique Phone
        IF EXISTS (SELECT 1 FROM dbo.Users WHERE Phone = @Phone)
        BEGIN
            SELECT 0 AS Success, 'This mobile number is already registered' AS Message, NULL AS UserId;
            RETURN;
        END

        -- Check Unique Email
        IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email)
        BEGIN
            SELECT 0 AS Success, 'This email address is already registered' AS Message, NULL AS UserId;
            RETURN;
        END

        -- Determine RoleId
        DECLARE @RoleId INT;
        SELECT @RoleId = RoleId FROM dbo.Roles WHERE RoleName = @RoleName;
        IF @RoleId IS NULL
        BEGIN
            SELECT @RoleId = RoleId FROM dbo.Roles WHERE RoleName = 'User';
            SET @RoleName = 'User';
        END

        -- Insert User Record
        DECLARE @NewUserId BIGINT;
        INSERT INTO dbo.Users (FullName, Email, PasswordHash, Phone, RoleId, IsActive, CreatedAt)
        VALUES (@FullName, @Email, @PasswordHash, @Phone, @RoleId, 1, SYSUTCDATETIME());

        SET @NewUserId = SCOPE_IDENTITY();

        -- Initialize User Wallet
        INSERT INTO dbo.UserWallets (UserId, Balance, Currency, UpdatedAt)
        VALUES (@NewUserId, 0.00, 'INR', SYSUTCDATETIME());

        -- If registering as Doctor, create Doctor profile with IsApproved = 0 (Pending Admin Approval)
        IF @RoleName = 'Doctor'
        BEGIN
            DECLARE @NewDoctorId BIGINT;
            INSERT INTO dbo.Doctors (
                UserId, 
                Department, 
                Specialty, 
                Qualification, 
                ExperienceYears, 
                ConsultationFeePerMinute, 
                IsFreeConsultation, 
                IsApproved, 
                IsLive, 
                CreatedAt
            )
            VALUES (
                @NewUserId,
                'General Medicine',
                'General Physician',
                'MBBS',
                1,
                25.00,
                0,
                0, -- IsApproved = 0: MUST BE VERIFIED AND ACTIVATED BY ADMIN
                0,
                SYSUTCDATETIME()
            );

            SET @NewDoctorId = SCOPE_IDENTITY();

            -- Initialize Doctor Wallet
            INSERT INTO dbo.DoctorWallets (DoctorId, Balance, TotalWithdrawn, UpdatedAt)
            VALUES (@NewDoctorId, 0.00, 0.00, SYSUTCDATETIME());

            SELECT 1 AS Success, 'Doctor registered successfully. Account pending Admin verification.' AS Message, @NewUserId AS UserId;
            RETURN;
        END

        SELECT 1 AS Success, 'User registered successfully' AS Message, @NewUserId AS UserId;
    END TRY
    BEGIN CATCH
        SELECT 0 AS Success, ERROR_MESSAGE() AS Message, NULL AS UserId;
    END CATCH
END
GO

-- 3. Stored Procedure for Admin to view all Users
CREATE OR ALTER PROCEDURE dbo.usp_Admin_GetUsers
    @Search NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        u.UserId,
        u.FullName,
        u.Email,
        ISNULL(u.Phone, '') AS Phone,
        r.RoleName,
        u.IsActive,
        u.CreatedAt,
        ISNULL(w.Balance, 0.00) AS WalletBalance,
        d.DoctorId,
        ISNULL(d.IsApproved, 0) AS IsDoctorApproved
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.UserWallets w ON u.UserId = w.UserId
    LEFT JOIN dbo.Doctors d ON u.UserId = d.UserId
    WHERE (@Search IS NULL OR u.FullName LIKE '%' + @Search + '%' OR u.Email LIKE '%' + @Search + '%' OR u.Phone LIKE '%' + @Search + '%')
    ORDER BY u.CreatedAt DESC;
END
GO

-- 4. Stored Procedure to Toggle User Active/Inactive Status
CREATE OR ALTER PROCEDURE dbo.usp_Admin_ToggleUserStatus
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
    SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END,
        UpdatedAt = SYSUTCDATETIME()
    WHERE UserId = @UserId;

    SELECT IsActive FROM dbo.Users WHERE UserId = @UserId;
END
GO
