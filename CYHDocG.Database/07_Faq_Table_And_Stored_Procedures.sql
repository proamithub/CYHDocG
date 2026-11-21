-- ============================================================================
-- CYHDocGuru: 07_Faq_Table_And_Stored_Procedures.sql
-- Pure English FAQ Architecture (Translations handled dynamically via Gemini AI)
-- ============================================================================

IF DB_ID('CYHDOCGDB') IS NOT NULL
BEGIN
    USE [CYHDOCGDB];
END
GO

-- 1. Create or Recreate FAQs Table (Pure English Schema)
IF OBJECT_ID('dbo.Faqs', 'U') IS NOT NULL
    DROP TABLE dbo.Faqs;
GO

CREATE TABLE dbo.Faqs (
    FaqId BIGINT IDENTITY(1,1) PRIMARY KEY,
    Category NVARCHAR(100) NOT NULL DEFAULT 'General',
    Question NVARCHAR(500) NOT NULL,
    Answer NVARCHAR(MAX) NOT NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NULL
);

CREATE NONCLUSTERED INDEX IX_Faqs_Active_Order ON dbo.Faqs (IsActive, DisplayOrder ASC)
INCLUDE (FaqId, Category, Question, Answer);
GO

-- 2. Stored Procedure: Get Active FAQs for Website Frontend
CREATE OR ALTER PROCEDURE dbo.usp_Faq_GetActive
    @Category NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        FaqId,
        Category,
        Question,
        Answer,
        DisplayOrder,
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM dbo.Faqs
    WHERE IsActive = 1
      AND (@Category IS NULL OR Category = @Category)
    ORDER BY DisplayOrder ASC, FaqId ASC;
END
GO

-- 3. Stored Procedure: Get All FAQs for Admin Desk
CREATE OR ALTER PROCEDURE dbo.usp_Faq_GetAllAdmin
    @SearchTerm NVARCHAR(200) = NULL,
    @Category NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        FaqId,
        Category,
        Question,
        Answer,
        DisplayOrder,
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM dbo.Faqs
    WHERE (@SearchTerm IS NULL 
           OR Question LIKE '%' + @SearchTerm + '%' 
           OR Answer LIKE '%' + @SearchTerm + '%')
      AND (@Category IS NULL OR Category = @Category)
    ORDER BY DisplayOrder ASC, FaqId DESC;
END
GO

-- 4. Stored Procedure: Get FAQ by Id
CREATE OR ALTER PROCEDURE dbo.usp_Faq_GetById
    @FaqId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        FaqId,
        Category,
        Question,
        Answer,
        DisplayOrder,
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM dbo.Faqs
    WHERE FaqId = @FaqId;
END
GO

-- 5. Stored Procedure: Insert / Update FAQ (Pure English)
CREATE OR ALTER PROCEDURE dbo.usp_Faq_Save
    @FaqId BIGINT = 0,
    @Category NVARCHAR(100) = 'General',
    @Question NVARCHAR(500),
    @Answer NVARCHAR(MAX),
    @DisplayOrder INT = 0,
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @FaqId > 0 AND EXISTS (SELECT 1 FROM dbo.Faqs WHERE FaqId = @FaqId)
    BEGIN
        UPDATE dbo.Faqs
        SET Category = @Category,
            Question = @Question,
            Answer = @Answer,
            DisplayOrder = @DisplayOrder,
            IsActive = @IsActive,
            UpdatedAt = SYSUTCDATETIME()
        WHERE FaqId = @FaqId;

        SELECT @FaqId AS FaqId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.Faqs (
            Category,
            Question,
            Answer,
            DisplayOrder,
            IsActive,
            CreatedAt
        )
        VALUES (
            @Category,
            @Question,
            @Answer,
            @DisplayOrder,
            @IsActive,
            SYSUTCDATETIME()
        );

        SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS FaqId;
    END
END
GO

-- 6. Stored Procedure: Toggle Active/Inactive Status
CREATE OR ALTER PROCEDURE dbo.usp_Faq_ToggleStatus
    @FaqId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Faqs
    SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END,
        UpdatedAt = SYSUTCDATETIME()
    WHERE FaqId = @FaqId;

    SELECT IsActive FROM dbo.Faqs WHERE FaqId = @FaqId;
END
GO

-- 7. Stored Procedure: Delete FAQ
CREATE OR ALTER PROCEDURE dbo.usp_Faq_Delete
    @FaqId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Faqs WHERE FaqId = @FaqId;
    SELECT @@ROWCOUNT AS RowsDeleted;
END
GO

-- 8. Seed Default Pure English FAQs
INSERT INTO dbo.Faqs (Category, Question, Answer, DisplayOrder, IsActive)
VALUES 
(
    'Appointments',
    'How can I book an appointment with CYHDocGuru?',
    'You can easily schedule a consultation online using our appointment form above, call our dedicated helpline at +1 (800) DOC-GURU, or email care@cyhdocguru.com. We confirm appointments within 2 hours.',
    1,
    1
),
(
    'Consultation',
    'What should I bring to my first cardiac consultation?',
    'Please bring any recent blood test reports, prior ECG or imaging records, your current medication list, and your photo ID with insurance coverage details.',
    2,
    1
),
(
    'Emergency',
    'Do you offer emergency medical consultations?',
    'Yes, our emergency helpline operates 24/7. If you experience severe chest heaviness, sudden shortness of breath, or palpitations, call +1 (800) DOC-GURU or visit our emergency wing immediately.',
    3,
    1
),
(
    'Telemedicine',
    'Are remote / telemedicine appointments supported?',
    'Yes, we provide secure video consultations for routine follow-ups, medication reviews, and second opinions, available Monday through Saturday.',
    4,
    1
),
(
    'Live Doctor',
    'How does 1-to-1 live doctor consultation work?',
    'You can browse active doctors on our platform, view their per-minute fee (or free consultation tag), and click "Connect". A live WebRTC video room opens up with an active dual-timer. Charges are deducted second-by-second from your wallet only for the exact duration of your call.',
    5,
    1
),
(
    'AI Triage',
    'What is AI Clinical Triage & Symptom Search?',
    'Our AI clinical engine analyzes your reported health symptoms (such as fever, chest pain, rash, hypertension) in English, determines the department and urgency level, and instantly recommends matching verified specialists.',
    6,
    1
);
GO
