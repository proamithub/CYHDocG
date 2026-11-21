-- Ensure every User with RoleId = 2 (Doctor) has a row in dbo.Doctors
INSERT INTO dbo.Doctors (UserId, Department, Specialty, Qualification, ExperienceYears, ConsultationFeePerMinute, IsFreeConsultation, IsApproved, IsLive, CreatedAt)
SELECT u.UserId, 'General Medicine', 'General Physician', 'MBBS', 1, 25.00, 0, 0, 0, SYSUTCDATETIME()
FROM dbo.Users u
LEFT JOIN dbo.Doctors d ON u.UserId = d.UserId
WHERE u.RoleId = 2 AND d.DoctorId IS NULL;

-- Ensure every Doctor has a DoctorWallet
INSERT INTO dbo.DoctorWallets (DoctorId, Balance, TotalWithdrawn, UpdatedAt)
SELECT d.DoctorId, 0.00, 0.00, SYSUTCDATETIME()
FROM dbo.Doctors d
LEFT JOIN dbo.DoctorWallets dw ON d.DoctorId = dw.DoctorId
WHERE dw.DoctorWalletId IS NULL;

-- Check list of Doctors
SELECT d.DoctorId, u.UserId, u.FullName, u.Email, u.Phone, d.IsApproved
FROM dbo.Users u
INNER JOIN dbo.Doctors d ON u.UserId = d.UserId;
GO
