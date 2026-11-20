using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Hubs;

public class LiveReelHub : Hub
{
    private readonly IDoctorRepository _doctorRepo;

    public LiveReelHub(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    public async Task JoinStreamRoom(string streamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Stream_{streamId}");
    }

    public async Task LeaveStreamRoom(string streamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Stream_{streamId}");
    }

    public async Task SendLiveComment(long streamId, string message)
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdStr, out long userId))
        {
            await Clients.Caller.SendAsync("AuthRequired", "Please log in to participate in live stream chat.");
            return;
        }

        if (string.IsNullOrWhiteSpace(message) || message.Length > 250)
        {
            return;
        }

        var result = await _doctorRepo.AddStreamCommentAsync(streamId, userId, message);
        if (result.Success && result.Data != null)
        {
            // Broadcast new comment to everyone watching this stream room
            await Clients.Group($"Stream_{streamId}").SendAsync("ReceiveNewComment", streamId, result.Data);
        }
        else
        {
            await Clients.Caller.SendAsync("CommentError", result.Message);
        }
    }

    public async Task SendLike(long streamId)
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdStr, out long userId))
        {
            await Clients.Caller.SendAsync("AuthRequired", "Please log in to like the live stream.");
            return;
        }

        var result = await _doctorRepo.ToggleStreamLikeAsync(streamId, userId);
        if (result.Success)
        {
            await Clients.Group($"Stream_{streamId}").SendAsync("ReceiveLikeUpdate", streamId, result.Data);
        }
    }

    public async Task ModerateBlockUser(long doctorId, long targetUserId, string reason)
    {
        var currentRole = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (currentRole != "Doctor" && currentRole != "Admin")
        {
            return;
        }

        await _doctorRepo.BlockUserAsync(doctorId, targetUserId, reason);
        await Clients.User(targetUserId.ToString()).SendAsync("UserBlockedFromChat", "You have been restricted from this stream by the doctor.");
    }
}
