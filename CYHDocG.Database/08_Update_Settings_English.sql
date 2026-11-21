IF EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'DefaultLanguage')
BEGIN
    UPDATE dbo.AppSettings SET SettingValue = 'English' WHERE SettingKey = 'DefaultLanguage';
END
ELSE
BEGIN
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description, IsActive)
    VALUES ('DefaultLanguage', 'English', 'Default UI language for public portal', 1);
END
GO
