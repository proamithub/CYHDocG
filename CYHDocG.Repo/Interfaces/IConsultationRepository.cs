using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IConsultationRepository
{
    Task<ApiResponse<long>> CreateConsultationRequestAsync(long userId, long doctorId);
    Task<ApiResponse<DateTime>> AcceptConsultationAsync(long consultationId, long doctorId);
    Task<ApiResponse<ConsultationEndDto>> EndAndDeductWalletAsync(long consultationId, long endedByUserId);
    Task<bool> IsParticipantAsync(long consultationId, long userId, string? role = null);
    Task<bool> IsDoctorAssignedAsync(long consultationId, long doctorUserId, long? doctorIdClaim = null);
    Task<ApiResponse> CancelOrDeclineConsultationAsync(long consultationId, long actorUserId, string status, bool isAdmin = false);
    Task<ApiResponse<ConsultationMessageDto>> SaveConsultationMessageAsync(long consultationId, long senderId, string senderRole, string messageText);
    Task<IEnumerable<ConsultationMessageDto>> GetConsultationMessagesAsync(long consultationId);
}
