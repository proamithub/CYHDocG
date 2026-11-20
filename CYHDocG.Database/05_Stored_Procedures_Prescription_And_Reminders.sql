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
