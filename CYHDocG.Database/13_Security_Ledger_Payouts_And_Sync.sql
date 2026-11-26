-- ============================================================================
-- CYHDocGuru Migration 13: Security, Payout Requests Schema, and Ledger Indexes
-- ============================================================================

IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. Ensure Doctors.UpdatedAt exists
IF COL_LENGTH('dbo.Doctors', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Doctors ADD UpdatedAt DATETIME2 NULL;
END
GO

-- 2. Create or update DoctorPayoutRequests table with Admin settlement fields
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
END
GO

-- 3. Useful covering indexes for Ledger and Consultation history
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PlatformLedger_Type_Created' AND object_id = OBJECT_ID('dbo.PlatformLedger'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PlatformLedger_Type_Created
    ON dbo.PlatformLedger (TransactionType, CreatedAt DESC)
    INCLUDE (Amount, PlatformFee, FromUserId, ToDoctorId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DoctorPayoutRequests_Status' AND object_id = OBJECT_ID('dbo.DoctorPayoutRequests'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DoctorPayoutRequests_Status
    ON dbo.DoctorPayoutRequests (Status, PayoutRequestId DESC)
    INCLUDE (DoctorId, Amount);
END
GO
