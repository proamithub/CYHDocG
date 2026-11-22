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
    IF EXISTS (
        SELECT 1 FROM dbo.Consultations c 
        INNER JOIN dbo.Doctors d ON c.DoctorId = d.DoctorId 
        WHERE c.ConsultationId = @ConsultationId 
          AND (c.DoctorId = @DoctorId OR d.UserId = @DoctorId) 
          AND c.Status = 'Pending'
    )
    BEGIN
        UPDATE dbo.Consultations
        SET Status = 'Active',
            StartedAt = SYSUTCDATETIME()
        WHERE ConsultationId = @ConsultationId;

        SELECT 1 AS Success, 'Consultation started' AS Message, SYSUTCDATETIME() AS StartedAt;
    END
    ELSE
    BEGIN
        DECLARE @ExistingStartedAt DATETIME2;
        SELECT @ExistingStartedAt = StartedAt FROM dbo.Consultations WHERE ConsultationId = @ConsultationId AND Status = 'Active';
        IF @ExistingStartedAt IS NOT NULL
        BEGIN
            SELECT 1 AS Success, 'Consultation already active' AS Message, @ExistingStartedAt AS StartedAt;
        END
        ELSE
        BEGIN
            SELECT 0 AS Success, 'Invalid or already processed consultation' AS Message, NULL AS StartedAt;
        END
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
        -- If consultation is Pending (not yet started), allow clean cancellation without billing
        IF EXISTS (SELECT 1 FROM dbo.Consultations WHERE ConsultationId = @ConsultationId AND Status = 'Pending')
        BEGIN
            UPDATE dbo.Consultations
            SET Status = 'Cancelled', EndedAt = SYSUTCDATETIME(), DurationSeconds = 0, TotalAmount = 0.00
            WHERE ConsultationId = @ConsultationId;

            COMMIT TRANSACTION;
            SELECT 1 AS Success, 'Consultation cancelled' AS Message, 0 AS DurationSeconds, 0.00 AS BilledAmount;
            RETURN;
        END

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
