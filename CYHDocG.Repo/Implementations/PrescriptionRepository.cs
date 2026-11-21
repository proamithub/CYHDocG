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
        string sql = "SELECT TOP (@Limit) MedicineId AS MasterMedicineId, BrandName, GenericName, DosageForm, Strength, Manufacturer FROM dbo.MasterMedicines ORDER BY BrandName ASC";
        return await connection.QueryAsync<MasterMedicineDto>(sql, new { Limit = limit });
    }

    public async Task<ApiResponse> AddMasterMedicineAsync(MasterMedicineDto dto)
    {
        using var connection = CreateConnection();
        string sql = @"
            INSERT INTO dbo.MasterMedicines (BrandName, GenericName, DosageForm, Strength, Manufacturer, CreatedAt)
            VALUES (@BrandName, @GenericName, @DosageForm, @Strength, @Manufacturer, SYSUTCDATETIME());";
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
        // 1. Create Prescription Master
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

        // 2. Add each medicine
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
}
