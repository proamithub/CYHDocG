using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IConsultationRepository
{
    Task<ApiResponse<long>> CreateConsultationRequestAsync(long userId, long doctorId);
    Task<ApiResponse<DateTime>> AcceptConsultationAsync(long consultationId, long doctorId);
    Task<ApiResponse<ConsultationEndDto>> EndAndDeductWalletAsync(long consultationId, long endedByUserId);
}
