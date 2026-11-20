using Dapper;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class ConsultationRepository : BaseRepository, IConsultationRepository
{
    public ConsultationRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<ApiResponse<long>> CreateConsultationRequestAsync(long userId, long doctorId)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Consultation_CreateRequest", new
        {
            UserId = userId,
            DoctorId = doctorId
        });

        if (result != null && (int)result.Success == 1)
        {
            return ApiResponse<long>.Ok((long)result.ConsultationId, (string)result.Message);
        }
        return ApiResponse<long>.Fail(result != null ? (string)result.Message : "Failed to create consultation request");
    }

    public async Task<ApiResponse<DateTime>> AcceptConsultationAsync(long consultationId, long doctorId)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Consultation_Accept", new
        {
            ConsultationId = consultationId,
            DoctorId = doctorId
        });

        if (result != null && (int)result.Success == 1)
        {
            return ApiResponse<DateTime>.Ok((DateTime)result.StartedAt, (string)result.Message);
        }
        return ApiResponse<DateTime>.Fail(result != null ? (string)result.Message : "Cannot accept consultation");
    }

    public async Task<ApiResponse<ConsultationEndDto>> EndAndDeductWalletAsync(long consultationId, long endedByUserId)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Consultation_EndAndDeductWallet", new
        {
            ConsultationId = consultationId,
            EndedByUserId = endedByUserId
        });

        if (result != null && (int)result.Success == 1)
        {
            var endDto = new ConsultationEndDto
            {
                ConsultationId = consultationId,
                DurationSeconds = (int)result.DurationSeconds,
                BilledAmount = (decimal)result.BilledAmount
            };
            return ApiResponse<ConsultationEndDto>.Ok(endDto, (string)result.Message);
        }
        return ApiResponse<ConsultationEndDto>.Fail(result != null ? (string)result.Message : "Failed to settle consultation");
    }
}
