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

    public async Task<bool> IsParticipantAsync(long consultationId, long userId, string? role = null)
    {
        if (consultationId <= 0 || userId <= 0) return false;
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return true;

        using var connection = CreateConnection();
        int count = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM dbo.Consultations c
            INNER JOIN dbo.Doctors d ON c.DoctorId = d.DoctorId
            WHERE c.ConsultationId = @ConsultationId
              AND (c.UserId = @UserId OR d.UserId = @UserId);",
            new { ConsultationId = consultationId, UserId = userId });

        return count > 0;
    }

    public async Task<bool> IsDoctorAssignedAsync(long consultationId, long doctorUserId, long? doctorIdClaim = null)
    {
        if (consultationId <= 0 || doctorUserId <= 0) return false;

        using var connection = CreateConnection();
        int count = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM dbo.Consultations c
            INNER JOIN dbo.Doctors d ON c.DoctorId = d.DoctorId
            WHERE c.ConsultationId = @ConsultationId
              AND (d.UserId = @DoctorUserId OR (@DoctorId IS NOT NULL AND c.DoctorId = @DoctorId));",
            new { ConsultationId = consultationId, DoctorUserId = doctorUserId, DoctorId = doctorIdClaim });

        return count > 0;
    }

    public async Task<ApiResponse> CancelOrDeclineConsultationAsync(long consultationId, long actorUserId, string status, bool isAdmin = false)
    {
        if (consultationId <= 0 || actorUserId <= 0)
        {
            return ApiResponse.Fail("Invalid consultation request.");
        }

        string normalizedStatus = string.Equals(status, "Declined", StringComparison.OrdinalIgnoreCase)
            ? "Declined"
            : "Cancelled";

        using var connection = CreateConnection();
        int rows = await connection.ExecuteAsync(@"
            UPDATE c
            SET c.Status = @Status,
                c.EndedAt = SYSUTCDATETIME(),
                c.DurationSeconds = 0,
                c.TotalAmount = 0.00
            FROM dbo.Consultations c
            INNER JOIN dbo.Doctors d ON c.DoctorId = d.DoctorId
            WHERE c.ConsultationId = @ConsultationId
              AND c.Status = 'Pending'
              AND (@IsAdmin = 1 OR c.UserId = @ActorUserId OR d.UserId = @ActorUserId);",
            new
            {
                ConsultationId = consultationId,
                ActorUserId = actorUserId,
                Status = normalizedStatus,
                IsAdmin = isAdmin ? 1 : 0
            });

        return rows > 0
            ? ApiResponse.Ok($"Consultation {normalizedStatus.ToLowerInvariant()}")
            : ApiResponse.Fail("Pending consultation not found or unauthorized");
    }

    public async Task<ApiResponse<ConsultationMessageDto>> SaveConsultationMessageAsync(long consultationId, long senderId, string senderRole, string messageText)
    {
        if (consultationId <= 0 || senderId <= 0 || string.IsNullOrWhiteSpace(messageText))
        {
            return ApiResponse<ConsultationMessageDto>.Fail("Invalid message payload");
        }

        string safeRole = string.Equals(senderRole, "Doctor", StringComparison.OrdinalIgnoreCase)
            ? "Doctor"
            : (string.Equals(senderRole, "AI", StringComparison.OrdinalIgnoreCase) ? "AI" : "User");

        string trimmed = messageText.Trim();
        using var connection = CreateConnection();

        var saved = await connection.QueryFirstOrDefaultAsync<ConsultationMessageDto>(@"
            INSERT INTO dbo.ConsultationMessages (ConsultationId, SenderId, SenderRole, MessageText, CreatedAt)
            OUTPUT INSERTED.MessageId, INSERTED.ConsultationId, INSERTED.SenderId, INSERTED.SenderRole, INSERTED.MessageText, INSERTED.CreatedAt
            VALUES (@ConsultationId, @SenderId, @SenderRole, @MessageText, SYSUTCDATETIME());",
            new
            {
                ConsultationId = consultationId,
                SenderId = senderId,
                SenderRole = safeRole,
                MessageText = trimmed
            });

        return saved != null
            ? ApiResponse<ConsultationMessageDto>.Ok(saved, "Saved")
            : ApiResponse<ConsultationMessageDto>.Fail("Failed to save message");
    }

    public async Task<IEnumerable<ConsultationMessageDto>> GetConsultationMessagesAsync(long consultationId)
    {
        if (consultationId <= 0) return Enumerable.Empty<ConsultationMessageDto>();

        using var connection = CreateConnection();
        return await connection.QueryAsync<ConsultationMessageDto>(@"
            SELECT MessageId, ConsultationId, SenderId, SenderRole, MessageText, CreatedAt
            FROM dbo.ConsultationMessages
            WHERE ConsultationId = @ConsultationId
            ORDER BY CreatedAt ASC;",
            new { ConsultationId = consultationId });
    }
}
