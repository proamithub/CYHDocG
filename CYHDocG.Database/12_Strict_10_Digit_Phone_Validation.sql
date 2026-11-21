-- 12_Strict_10_Digit_Phone_Validation.sql
-- Enforce strict 10-digit mobile number validation and normalization in database

USE CYHDOCGDB;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_RegisterUser
    @FullName NVARCHAR(150),
    @Email NVARCHAR(150),
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
            SELECT 0 AS Success, 'Mobile number is mandatory' AS Message, NULL AS UserId;
            RETURN;
        END

        IF @Email IS NULL OR LTRIM(RTRIM(@Email)) = ''
        BEGIN
            SELECT 0 AS Success, 'Email address is required' AS Message, NULL AS UserId;
            RETURN;
        END

        SET @FullName = LTRIM(RTRIM(@FullName));
        SET @Email = LOWER(LTRIM(RTRIM(@Email)));
        SET @Phone = LTRIM(RTRIM(@Phone));

        -- Normalize Phone Number: remove spaces, +, -, (, )
        SET @Phone = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@Phone, ' ', ''), '-', ''), '+', ''), '(', ''), ')', '');

        -- Strip leading 91 if 12 digits (Indian country code)
        IF LEN(@Phone) = 12 AND LEFT(@Phone, 2) = '91'
        BEGIN
            SET @Phone = SUBSTRING(@Phone, 3, 10);
        END
        -- Strip leading 0 if 11 digits
        ELSE IF LEN(@Phone) = 11 AND LEFT(@Phone, 1) = '0'
        BEGIN
            SET @Phone = SUBSTRING(@Phone, 2, 10);
        END

        -- Strict 10-digit real mobile checks
        IF LEN(@Phone) <> 10 OR @Phone LIKE '%[^0-9]%'
        BEGIN
            SELECT 0 AS Success, 'Mobile number must be exactly 10 digits' AS Message, NULL AS UserId;
            RETURN;
        END

        IF LEFT(@Phone, 1) NOT IN ('6', '7', '8', '9')
        BEGIN
            SELECT 0 AS Success, 'Mobile number must start with 6, 7, 8, or 9' AS Message, NULL AS UserId;
            RETURN;
        END

        -- Reject all identical digits (e.g. 0000000000, 1111111111, ..., 9999999999)
        IF LEN(REPLACE(@Phone, LEFT(@Phone, 1), '')) = 0
        BEGIN
            SELECT 0 AS Success, 'Invalid mobile number (all digits cannot be identical)' AS Message, NULL AS UserId;
            RETURN;
        END

        -- Reject sequential dummy patterns
        IF @Phone = '1234567890' OR @Phone = '0123456789'
        BEGIN
            SELECT 0 AS Success, 'Sequential dummy numbers like 1234567890 are not permitted' AS Message, NULL AS UserId;
            RETURN;
        END

        -- Check Unique Phone
        IF EXISTS (SELECT 1 FROM dbo.Users WHERE Phone = @Phone)
        BEGIN
            SELECT 0 AS Success, 'This mobile number is already registered' AS Message, NULL AS UserId;
            RETURN;
        END

        -- Check Unique Email
        IF EXISTS (SELECT 1 FROM dbo.Users WHERE LOWER(Email) = @Email)
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

        -- Insert User Record with cleaned 10-digit Phone
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
