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
    VALUES ('DefaultLanguage', 'Hindi', 'Default UI language for public portal', 1);

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
