using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Hubs;

[Authorize]
public class ConsultationHub : Hub
{
    private readonly IConsultationRepository _consultationRepo;

    public ConsultationHub(IConsultationRepository consultationRepo)
    {
        _consultationRepo = consultationRepo;
    }

    public async Task JoinConsultation(string consultationId)
    {
        if (!long.TryParse(consultationId, out long cid) || cid <= 0) return;
        if (!long.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out long userId)) return;

        string role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? "User";
        if (!await _consultationRepo.IsParticipantAsync(cid, userId, role))
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"Consultation_{cid}");
    }

    public async Task SendMessage(long consultationId, string message, string senderRole)
    {
        if (consultationId <= 0 || string.IsNullOrWhiteSpace(message)) return;
        if (!long.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out long senderId) || senderId <= 0) return;

        string role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? "User";
        if (!await _consultationRepo.IsParticipantAsync(consultationId, senderId, role))
        {
            return;
        }

        string effectiveRole = role == "Doctor" ? "Doctor" : "User";
        var saved = await _consultationRepo.SaveConsultationMessageAsync(consultationId, senderId, effectiveRole, message);

        var payload = new
        {
            senderId = senderId,
            senderRole = effectiveRole,
            messageText = saved.Data?.MessageText ?? message.Trim(),
            createdAt = saved.Data?.CreatedAt ?? DateTime.UtcNow
        };

        await Clients.OthersInGroup($"Consultation_{consultationId}").SendAsync("ReceiveConsultationMessage", payload);
    }

    public async Task BroadcastTimerStart(long consultationId, DateTime startedAt, decimal ratePerMinute, bool isPaid)
    {
        if (!long.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId)) return;
        string role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? "User";
        long? doctorIdClaim = long.TryParse(Context.User?.FindFirstValue("DoctorId"), out long dc) ? dc : null;

        if (role != "Admin" && !await _consultationRepo.IsDoctorAssignedAsync(consultationId, callerUserId, doctorIdClaim))
        {
            return;
        }

        await Clients.Group($"Consultation_{consultationId}").SendAsync("TimerStarted", new
        {
            startedAt = startedAt,
            ratePerMinute = ratePerMinute,
            isPaid = isPaid
        });
    }

    public async Task EndSession(long consultationId)
    {
        if (!long.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out long endedByUserId) || endedByUserId <= 0) return;

        string role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? "User";
        if (!await _consultationRepo.IsParticipantAsync(consultationId, endedByUserId, role))
        {
            return;
        }

        var result = await _consultationRepo.EndAndDeductWalletAsync(consultationId, endedByUserId);
        if (result.Success && result.Data != null)
        {
            await Clients.Group($"Consultation_{consultationId}").SendAsync("ConsultationEnded", result.Data);
        }
    }
}
