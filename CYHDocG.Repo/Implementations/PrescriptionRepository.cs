using Dapper;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class PrescriptionRepository : BaseRepository, IPrescriptionRepository
{
    public PrescriptionRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<IEnumerable<MasterMedicineDto>> SearchMasterMedicinesAsync(string query)
    {
        return await QuerySPAsync<MasterMedicineDto>("dbo.usp_MasterMedicine_Search", new { Query = query });
    }

    public async Task<IEnumerable<MasterMedicineDto>> GetAllMasterMedicinesAsync(int limit = 100)
    {
        using var connection = CreateConnection();
        string sql = @"
            SELECT TOP (@Limit)
                MasterMedicineId,
                BrandName,
                GenericName,
                DosageForm,
                Strength,
                Manufacturer
            FROM dbo.MasterMedicines
            WHERE IsActive = 1
            ORDER BY BrandName ASC;";
        return await connection.QueryAsync<MasterMedicineDto>(sql, new { Limit = limit });
    }

    public async Task<ApiResponse> AddMasterMedicineAsync(MasterMedicineDto dto)
    {
        using var connection = CreateConnection();
        string sql = @"
            INSERT INTO dbo.MasterMedicines (BrandName, GenericName, DosageForm, Strength, Manufacturer, IsActive)
            VALUES (@BrandName, @GenericName, @DosageForm, @Strength, @Manufacturer, 1);";
        await connection.ExecuteAsync(sql, new
        {
            BrandName = dto.BrandName.Trim(),
            GenericName = dto.GenericName?.Trim(),
            DosageForm = string.IsNullOrWhiteSpace(dto.DosageForm) ? "Tablet" : dto.DosageForm.Trim(),
            Strength = dto.Strength?.Trim(),
            Manufacturer = dto.Manufacturer?.Trim()
        });
        return ApiResponse.Ok("Medicine added to catalog successfully");
    }

    public async Task<ApiResponse<long>> CreatePrescriptionAsync(PrescriptionCreateDto request)
    {
        var masterResult = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Prescription_Create", new
        {
            ConsultationId = request.ConsultationId,
            DoctorId = request.DoctorId,
            UserId = request.UserId,
            OriginalFileUrl = request.OriginalFileUrl,
            CompressedFileUrl = request.CompressedFileUrl,
            OcrExtractedText = request.OcrExtractedText,
            DoctorNotes = request.DoctorNotes
        });

        if (masterResult == null || (int)masterResult.Success != 1)
        {
            return ApiResponse<long>.Fail("Failed to save prescription header");
        }

        long prescriptionId = (long)masterResult.PrescriptionId;

        foreach (var med in request.Medicines)
        {
            await ExecuteSPAsync("dbo.usp_Prescription_AddMedicine", new
            {
                PrescriptionId = prescriptionId,
                MedicineName = med.MedicineName,
                DosageSchedule = med.DosageSchedule,
                TimingInstruction = med.TimingInstruction,
                DurationDays = med.DurationDays,
                TotalUnits = med.TotalUnits
            });
        }

        return ApiResponse<long>.Ok(prescriptionId, "Prescription created with reminders");
    }

    public async Task<IEnumerable<PrescriptionHeaderDto>> GetUserPrescriptionsAsync(long userId)
    {
        using var connection = CreateConnection();
        using var multi = await connection.QueryMultipleAsync("dbo.usp_Prescription_GetByUserId", new { UserId = userId }, commandType: System.Data.CommandType.StoredProcedure);

        var headers = (await multi.ReadAsync<PrescriptionHeaderDto>()).ToList();
        var medicines = (await multi.ReadAsync<PrescriptionMedicineItemDto>()).ToList();

        foreach (var header in headers)
        {
            header.Medicines = medicines.Where(m => m.PrescriptionId == header.PrescriptionId).ToList();
        }

        return headers;
    }

    public async Task<IEnumerable<DoseReminderItemDto>> GetUserTodayDoseRemindersAsync(long userId)
    {
        if (userId <= 0) return Enumerable.Empty<DoseReminderItemDto>();

        using var connection = CreateConnection();
        DateTime today = DateTime.UtcNow.Date;

        // Fetch active prescribed medicines for this user and ensure today's dose slots exist
        var activeMeds = (await connection.QueryAsync<PrescriptionMedicineItemDto>(@"
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
              AND pm.CourseEndDate >= @Today;",
            new { UserId = userId, Today = today })).ToList();

        if (!activeMeds.Any())
        {
            return Enumerable.Empty<DoseReminderItemDto>();
        }

        var existingToday = (await connection.QueryAsync<(long ReminderId, long PrescriptionMedicineId, DateTime ReminderDateTime)>(@"
            SELECT ReminderId, PrescriptionMedicineId, ReminderDateTime
            FROM dbo.MedicineReminders
            WHERE UserId = @UserId
              AND Status IN ('Pending', 'Taken', 'Missed')
              AND ReminderDateTime >= @TodayStart
              AND ReminderDateTime < @TodayEnd;",
            new { UserId = userId, TodayStart = today, TodayEnd = today.AddDays(1) })).ToList();

        foreach (var med in activeMeds)
        {
            var parts = (med.DosageSchedule ?? "1-0-1").Split('-');
            bool hasMorning = parts.Length > 0 && int.TryParse(parts[0], out int m) && m > 0;
            bool hasAfternoon = parts.Length > 1 && int.TryParse(parts[1], out int a) && a > 0;
            bool hasNight = parts.Length > 2 && int.TryParse(parts[2], out int n) && n > 0;

            await EnsureSlotReminderAsync(connection, userId, med.PrescriptionMedicineId, today.AddHours(8), hasMorning, existingToday);
            await EnsureSlotReminderAsync(connection, userId, med.PrescriptionMedicineId, today.AddHours(13), hasAfternoon, existingToday);
            await EnsureSlotReminderAsync(connection, userId, med.PrescriptionMedicineId, today.AddHours(20).AddMinutes(30), hasNight, existingToday);
        }

        var rows = await connection.QueryAsync<DoseReminderItemDto>(@"
            SELECT 
                r.ReminderId,
                r.PrescriptionMedicineId,
                pm.MedicineName,
                pm.DosageSchedule,
                pm.TimingInstruction,
                r.ReminderDateTime,
                r.Status
            FROM dbo.MedicineReminders r
            INNER JOIN dbo.PrescriptionMedicines pm ON r.PrescriptionMedicineId = pm.PrescriptionMedicineId
            WHERE r.UserId = @UserId
              AND r.Status IN ('Pending', 'Taken', 'Missed')
              AND r.ReminderDateTime >= @TodayStart
              AND r.ReminderDateTime < @TodayEnd
              AND pm.CourseEndDate >= @TodayStart
            ORDER BY r.ReminderDateTime ASC, pm.MedicineName ASC;",
            new { UserId = userId, TodayStart = today, TodayEnd = today.AddDays(1) });

        var list = rows.ToList();
        foreach (var item in list)
        {
            int hour = item.ReminderDateTime.Hour;
            if (hour < 12)
            {
                item.Slot = "Morning";
                item.SlotTimeLabel = "08:00 AM";
            }
            else if (hour < 17)
            {
                item.Slot = "Afternoon";
                item.SlotTimeLabel = "01:00 PM";
            }
            else
            {
                item.Slot = "Night";
                item.SlotTimeLabel = "08:30 PM";
            }
        }

        return list;
    }

    private static async Task EnsureSlotReminderAsync(
        System.Data.IDbConnection connection,
        long userId,
        long rxMedId,
        DateTime slotTime,
        bool shouldExist,
        List<(long ReminderId, long PrescriptionMedicineId, DateTime ReminderDateTime)> existingToday)
    {
        if (!shouldExist) return;

        bool alreadyExists = existingToday.Any(e =>
            e.PrescriptionMedicineId == rxMedId &&
            e.ReminderDateTime.Hour == slotTime.Hour);

        if (!alreadyExists)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO dbo.MedicineReminders (PrescriptionMedicineId, UserId, ReminderDateTime, Status)
                VALUES (@RxMedId, @UserId, @ReminderDateTime, 'Pending');",
                new { RxMedId = rxMedId, UserId = userId, ReminderDateTime = slotTime });
        }
    }

    public async Task<ApiResponse> MarkReminderStatusAsync(long reminderId, long userId, string status)
    {
        if (reminderId <= 0 || userId <= 0)
        {
            return ApiResponse.Fail("Invalid reminder");
        }

        string safeStatus = string.Equals(status, "Taken", StringComparison.OrdinalIgnoreCase)
            ? "Taken"
            : (string.Equals(status, "Missed", StringComparison.OrdinalIgnoreCase) ? "Missed" : "Pending");

        using var connection = CreateConnection();
        int rows = await connection.ExecuteAsync(@"
            UPDATE dbo.MedicineReminders
            SET Status = @Status,
                NotifiedAt = SYSUTCDATETIME()
            WHERE ReminderId = @ReminderId AND UserId = @UserId;",
            new { ReminderId = reminderId, UserId = userId, Status = safeStatus });

        return rows > 0
            ? ApiResponse.Ok($"Dose marked as {safeStatus}")
            : ApiResponse.Fail("Reminder not found");
    }

    public async Task<IEnumerable<PatientSummaryDto>> SearchPatientsForDoctorAsync(string query)
    {
        string mobile10 = NormalizeMobileQueryDigits(query);
        if (mobile10.Length != 10) return Enumerable.Empty<PatientSummaryDto>();

        using var connection = CreateConnection();
        return await connection.QueryAsync<PatientSummaryDto>(@"
            SELECT TOP 5
                u.UserId,
                u.FullName,
                u.Email,
                ISNULL(u.Phone, '') AS Phone
            FROM dbo.Users u
            INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
            WHERE u.IsActive = 1
              AND r.RoleName = 'User'
              AND u.Phone IS NOT NULL
              AND u.Phone <> ''
              AND (
                  u.Phone = @Mobile10
                  OR REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(u.Phone, ' ', ''), '-', ''), '+', ''), '(', ''), ')', '') IN (@Mobile10, '0' + @Mobile10, '91' + @Mobile10)
              )
            ORDER BY u.FullName ASC;",
            new { Mobile10 = mobile10 });
    }

    private static string NormalizeMobileQueryDigits(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
        {
            return digits[2..];
        }
        if (digits.Length == 11 && digits.StartsWith("0", StringComparison.Ordinal))
        {
            return digits[1..];
        }

        return digits;
    }

    public async Task<IEnumerable<PrescriptionHeaderDto>> GetIssuedPrescriptionsForDoctorAsync(
        long doctorId,
        long? userId = null,
        long? consultationId = null)
    {
        if (doctorId <= 0 && (!userId.HasValue || userId.Value <= 0))
        {
            return Enumerable.Empty<PrescriptionHeaderDto>();
        }

        using var connection = CreateConnection();
        bool hasTargetPatient = userId.HasValue && userId.Value > 0;

        string headerSql = hasTargetPatient
            ? @"
                SELECT TOP 25
                    p.PrescriptionId,
                    p.ConsultationId,
                    p.DoctorId,
                    p.UserId,
                    p.CreatedAt,
                    p.CompressedFileUrl,
                    p.DoctorNotes,
                    d.Specialty,
                    docUser.FullName AS DoctorName,
                    pat.FullName AS PatientName,
                    ISNULL(pat.Phone, '') AS PatientPhone,
                    ISNULL(pat.Email, '') AS PatientEmail
                FROM dbo.Prescriptions p
                INNER JOIN dbo.Doctors d ON p.DoctorId = d.DoctorId
                INNER JOIN dbo.Users docUser ON d.UserId = docUser.UserId
                INNER JOIN dbo.Users pat ON p.UserId = pat.UserId
                WHERE p.UserId = @UserId
                ORDER BY
                    CASE WHEN @ConsultationId IS NOT NULL AND p.ConsultationId = @ConsultationId THEN 0 ELSE 1 END,
                    p.CreatedAt DESC;"
            : @"
                SELECT TOP 20
                    p.PrescriptionId,
                    p.ConsultationId,
                    p.DoctorId,
                    p.UserId,
                    p.CreatedAt,
                    p.CompressedFileUrl,
                    p.DoctorNotes,
                    d.Specialty,
                    docUser.FullName AS DoctorName,
                    pat.FullName AS PatientName,
                    ISNULL(pat.Phone, '') AS PatientPhone,
                    ISNULL(pat.Email, '') AS PatientEmail
                FROM dbo.Prescriptions p
                INNER JOIN dbo.Doctors d ON p.DoctorId = d.DoctorId
                INNER JOIN dbo.Users docUser ON d.UserId = docUser.UserId
                INNER JOIN dbo.Users pat ON p.UserId = pat.UserId
                WHERE p.DoctorId = @DoctorId
                ORDER BY p.CreatedAt DESC;";

        var headers = (await connection.QueryAsync<PrescriptionHeaderDto>(headerSql, new
        {
            DoctorId = doctorId,
            UserId = userId,
            ConsultationId = consultationId
        })).ToList();

        if (!headers.Any())
        {
            return headers;
        }

        var prescriptionIds = headers.Select(h => h.PrescriptionId).ToArray();
        var medicines = (await connection.QueryAsync<PrescriptionMedicineItemDto>(@"
            SELECT
                PrescriptionMedicineId,
                PrescriptionId,
                MedicineName,
                DosageSchedule,
                TimingInstruction,
                DurationDays,
                CourseEndDate,
                RefillAlertDate
            FROM dbo.PrescriptionMedicines
            WHERE PrescriptionId IN @PrescriptionIds
            ORDER BY PrescriptionMedicineId ASC;",
            new { PrescriptionIds = prescriptionIds })).ToList();

        var medsByRx = medicines.GroupBy(m => m.PrescriptionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var header in headers)
        {
            if (medsByRx.TryGetValue(header.PrescriptionId, out var list))
            {
                header.Medicines = list;
            }
        }

        return headers;
    }
}
