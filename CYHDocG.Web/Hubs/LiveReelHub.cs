using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Hubs;

public class LiveReelHub : Hub
{
    private readonly IDoctorRepository _doctorRepo;

    // Room viewers: roomKey -> Set of ConnectionIds
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _roomViewers = new();

    // Connection to room mapping: connectionId -> roomKey
    private static readonly ConcurrentDictionary<string, string> _connectionRooms = new();

    // Pinned Banner cache: streamKey -> PinnedBannerDto
    private static readonly ConcurrentDictionary<string, PinnedBannerDto> _roomPinnedBanners = new();

    public LiveReelHub(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    private static string NormalizeKey(string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId)) return "1";
        return streamId.Replace("stream_doc_", "").Replace("doc_", "").Trim();
    }

    public async Task JoinStreamRoom(string streamId)
    {
        string key = NormalizeKey(streamId);
        string connId = Context.ConnectionId;

        // Leave previous room if any
        if (_connectionRooms.TryRemove(connId, out var oldKey) && oldKey != key)
        {
            await Groups.RemoveFromGroupAsync(connId, $"Stream_{oldKey}");
            if (_roomViewers.TryGetValue(oldKey, out var oldViewers))
            {
                oldViewers.TryRemove(connId, out _);
                int count = oldViewers.Count;
                await Clients.Group($"Stream_{oldKey}").SendAsync("UpdateViewerCount", oldKey, count);
                NotifyViewerCountDb(oldKey, count);
            }
        }

        // Add to new room
        _connectionRooms[connId] = key;
        var viewers = _roomViewers.GetOrAdd(key, _ => new ConcurrentDictionary<string, bool>());
        viewers[connId] = true;

        await Groups.AddToGroupAsync(connId, $"Stream_{key}");

        int currentCount = viewers.Count;
        // Broadcast to all viewers and doctor in this stream room
        await Clients.Group($"Stream_{key}").SendAsync("UpdateViewerCount", key, currentCount);
        await Clients.Caller.SendAsync("UpdateViewerCount", key, currentCount);

        // If there is an active pinned banner for this room, send to the joining viewer
        if (_roomPinnedBanners.TryGetValue(key, out var activeBanner))
        {
            await Clients.Caller.SendAsync("ReceivePinnedBanner", key, activeBanner);
        }

        NotifyViewerCountDb(key, currentCount);
    }

    public async Task LeaveStreamRoom(string streamId)
    {
        string key = NormalizeKey(streamId);
        string connId = Context.ConnectionId;

        _connectionRooms.TryRemove(connId, out _);
        await Groups.RemoveFromGroupAsync(connId, $"Stream_{key}");

        if (_roomViewers.TryGetValue(key, out var viewers))
        {
            viewers.TryRemove(connId, out _);
            int count = viewers.Count;
            await Clients.Group($"Stream_{key}").SendAsync("UpdateViewerCount", key, count);
            NotifyViewerCountDb(key, count);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connId = Context.ConnectionId;
        if (_connectionRooms.TryRemove(connId, out var key))
        {
            if (_roomViewers.TryGetValue(key, out var viewers))
            {
                viewers.TryRemove(connId, out _);
                int count = viewers.Count;
                await Clients.Group($"Stream_{key}").SendAsync("UpdateViewerCount", key, count);
                NotifyViewerCountDb(key, count);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private void NotifyViewerCountDb(string key, int count)
    {
        if (long.TryParse(key, out long id))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _doctorRepo.UpdateStreamViewerCountAsync(id, count);
                }
                catch { }
            });
        }
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
            string key = NormalizeKey(streamId.ToString());
            // Broadcast new comment to everyone watching this stream room
            await Clients.Group($"Stream_{key}").SendAsync("ReceiveNewComment", streamId, result.Data);
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
            string key = NormalizeKey(streamId.ToString());
            await Clients.Group($"Stream_{key}").SendAsync("ReceiveLikeUpdate", streamId, result.Data);
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

    public async Task PinStreamBanner(long doctorId, string bannerText, string bannerType, string? medicineName)
    {
        if (string.IsNullOrWhiteSpace(bannerText)) return;

        string key = NormalizeKey(doctorId.ToString());
        var banner = new PinnedBannerDto
        {
            DoctorId = doctorId,
            BannerText = bannerText.Trim(),
            BannerType = string.IsNullOrWhiteSpace(bannerType) ? "tip" : bannerType.Trim(),
            MedicineName = string.IsNullOrWhiteSpace(medicineName) ? null : medicineName.Trim(),
            PinnedAt = DateTime.UtcNow
        };

        _roomPinnedBanners[key] = banner;

        // Broadcast to everyone currently watching this doctor's stream (both doctor and all patients)
        await Clients.Group($"Stream_{key}").SendAsync("ReceivePinnedBanner", key, banner);
    }

    public async Task UnpinStreamBanner(long doctorId)
    {
        string key = NormalizeKey(doctorId.ToString());
        _roomPinnedBanners.TryRemove(key, out _);
        await Clients.Group($"Stream_{key}").SendAsync("ClearPinnedBanner", key);
    }
}
