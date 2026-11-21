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
}
