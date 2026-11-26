using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IPrescriptionRepository
{
    Task<IEnumerable<MasterMedicineDto>> SearchMasterMedicinesAsync(string query);
    Task<IEnumerable<MasterMedicineDto>> GetAllMasterMedicinesAsync(int limit = 100);
    Task<ApiResponse> AddMasterMedicineAsync(MasterMedicineDto dto);
    Task<ApiResponse<long>> CreatePrescriptionAsync(PrescriptionCreateDto request);
    Task<IEnumerable<PrescriptionHeaderDto>> GetUserPrescriptionsAsync(long userId);
    Task<IEnumerable<DoseReminderItemDto>> GetUserTodayDoseRemindersAsync(long userId);
    Task<ApiResponse> MarkReminderStatusAsync(long reminderId, long userId, string status);
    Task<IEnumerable<PatientSummaryDto>> SearchPatientsForDoctorAsync(string query);
    Task<IEnumerable<PrescriptionHeaderDto>> GetIssuedPrescriptionsForDoctorAsync(long doctorId, long? userId = null, long? consultationId = null);
}
