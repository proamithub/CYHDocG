using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IPrescriptionRepository
{
    Task<IEnumerable<MasterMedicineDto>> SearchMasterMedicinesAsync(string query);
    Task<ApiResponse<long>> CreatePrescriptionAsync(PrescriptionCreateDto request);
    Task<IEnumerable<PrescriptionHeaderDto>> GetUserPrescriptionsAsync(long userId);
}
