using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Hubs;

public class ConsultationHub : Hub
{
    private readonly IConsultationRepository _consultationRepo;

    public ConsultationHub(IConsultationRepository consultationRepo)
    {
        _consultationRepo = consultationRepo;
    }

    public async Task JoinConsultation(string consultationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Consultation_{consultationId}");
    }

    public async Task SendMessage(long consultationId, string message, string senderRole)
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(userIdStr, out long senderId);

        var payload = new
        {
            SenderId = senderId,
            SenderRole = senderRole,
            MessageText = message,
            CreatedAt = DateTime.UtcNow
        };

        // Broadcast to other participant in the consultation room
        await Clients.OthersInGroup($"Consultation_{consultationId}").SendAsync("ReceiveConsultationMessage", payload);
    }

    public async Task BroadcastTimerStart(long consultationId, DateTime startedAt, decimal ratePerMinute, bool isPaid)
    {
        await Clients.Group($"Consultation_{consultationId}").SendAsync("TimerStarted", new
        {
            StartedAt = startedAt,
            RatePerMinute = ratePerMinute,
            IsPaid = isPaid
        });
    }

    public async Task EndSession(long consultationId)
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(userIdStr, out long endedByUserId);

        var result = await _consultationRepo.EndAndDeductWalletAsync(consultationId, endedByUserId);
        if (result.Success && result.Data != null)
        {
            await Clients.Group($"Consultation_{consultationId}").SendAsync("ConsultationEnded", result.Data);
        }
    }
}
