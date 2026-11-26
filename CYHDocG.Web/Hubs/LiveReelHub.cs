using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Hubs;

public class LiveReelHub : Hub
{
    private readonly IDoctorRepository _doctorRepo;
    private readonly IConsultationRepository _consultationRepo;

    // Room viewers: roomKey -> Set of ConnectionIds
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _roomViewers = new();

    // Connection to room mapping: connectionId -> roomKey
    private static readonly ConcurrentDictionary<string, string> _connectionRooms = new();

    // Pinned Banner cache: streamKey -> PinnedBannerDto
    private static readonly ConcurrentDictionary<string, PinnedBannerDto> _roomPinnedBanners = new();

    // Doctor live broadcaster tracking: doctorId <-> connectionId
    private static readonly ConcurrentDictionary<long, string> _doctorLiveConnections = new();
    private static readonly ConcurrentDictionary<string, long> _connectionToDoctors = new();

    public LiveReelHub(IDoctorRepository doctorRepo, IConsultationRepository consultationRepo)
    {
        _doctorRepo = doctorRepo;
        _consultationRepo = consultationRepo;
    }

    private async Task<bool> IsAuthorizedDoctorOwnerAsync(long doctorId)
    {
        string? role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (role == "Admin") return true;
        if (role != "Doctor") return false;

        if (!long.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return false;
        }

        if (long.TryParse(Context.User?.FindFirstValue("DoctorId"), out long claimDocId) && claimDocId == doctorId)
        {
            return true;
        }

        return await _doctorRepo.IsDoctorUserOwnerAsync(doctorId, callerUserId);
    }

    [Authorize(Roles = "Doctor,Admin")]
    public async Task RegisterDoctorBroadcaster(long doctorId)
    {
        if (!await IsAuthorizedDoctorOwnerAsync(doctorId)) return;

        string connId = Context.ConnectionId;
        _doctorLiveConnections[doctorId] = connId;
        _connectionToDoctors[connId] = doctorId;

        string key = NormalizeKey(doctorId.ToString());
        await Groups.AddToGroupAsync(connId, $"Stream_{key}");

        // Doctor is broadcaster, not viewer; send current active patient viewer count to doctor
        int count = _roomViewers.TryGetValue(key, out var viewers) ? viewers.Count : 0;
        await Clients.Caller.SendAsync("UpdateViewerCount", key, count);
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
        bool isDoctorBroadcaster = _connectionToDoctors.ContainsKey(connId);

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

        // Count in viewer stats only if patient (not doctor broadcaster)
        if (!isDoctorBroadcaster)
        {
            viewers[connId] = true;
        }

        await Groups.AddToGroupAsync(connId, $"Stream_{key}");

        int currentCount = viewers.Count;
        await Clients.Group($"Stream_{key}").SendAsync("UpdateViewerCount", key, currentCount);
        await Clients.Caller.SendAsync("UpdateViewerCount", key, currentCount);

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

        if (_connectionToDoctors.TryRemove(connId, out long docId))
        {
            _doctorLiveConnections.TryRemove(docId, out _);
            _ = Task.Run(async () =>
            {
                try
                {
                    await _doctorRepo.SetDoctorLiveStatusAsync(docId, false, null);
                    await Clients.All.SendAsync("DoctorLiveStatusChanged", new
                    {
                        doctorId = docId,
                        isLive = false,
                        doctorName = "",
                        streamUrl = ""
                    });
                }
                catch { }
            });
        }

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

    [Authorize(Roles = "Doctor,Admin")]
    public async Task ModerateBlockUser(long doctorId, long targetUserId, string reason)
    {
        if (!await IsAuthorizedDoctorOwnerAsync(doctorId)) return;

        await _doctorRepo.BlockUserAsync(doctorId, targetUserId, reason);
        await Clients.User(targetUserId.ToString()).SendAsync("UserBlockedFromChat", "You have been restricted from this stream by the doctor.");

        string key = NormalizeKey(doctorId.ToString());
        await Clients.Group($"Stream_{key}").SendAsync("UserRemovedFromChat", new { doctorId, targetUserId });
    }

    [Authorize(Roles = "Doctor,Admin")]
    public async Task ModerateUnblockUser(long doctorId, long targetUserId)
    {
        if (!await IsAuthorizedDoctorOwnerAsync(doctorId)) return;

        await _doctorRepo.UnblockUserAsync(doctorId, targetUserId);
        await Clients.User(targetUserId.ToString()).SendAsync("UserUnblockedFromChat", "Your chat restriction has been lifted by the doctor.");
    }

    [Authorize(Roles = "Doctor,Admin")]
    public async Task DeleteComment(long doctorId, long commentId)
    {
        if (!await IsAuthorizedDoctorOwnerAsync(doctorId)) return;

        bool isAdmin = Context.User?.IsInRole("Admin") == true;
        await _doctorRepo.DeleteStreamCommentAsync(doctorId, commentId, isAdmin);

        string key = NormalizeKey(doctorId.ToString());
        await Clients.Group($"Stream_{key}").SendAsync("CommentDeleted", commentId);
    }

    [Authorize(Roles = "Doctor,Admin")]
    public async Task PinStreamBanner(long doctorId, string bannerText, string bannerType, string? medicineName)
    {
        if (!await IsAuthorizedDoctorOwnerAsync(doctorId)) return;
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
        await Clients.Group($"Stream_{key}").SendAsync("ReceivePinnedBanner", key, banner);
    }

    [Authorize(Roles = "Doctor,Admin")]
    public async Task UnpinStreamBanner(long doctorId)
    {
        if (!await IsAuthorizedDoctorOwnerAsync(doctorId)) return;

        string key = NormalizeKey(doctorId.ToString());
        _roomPinnedBanners.TryRemove(key, out _);
        await Clients.Group($"Stream_{key}").SendAsync("ClearPinnedBanner", key);
    }

    [Authorize(Roles = "Doctor,Admin")]
    public async Task DeclineConsultationRequest(long doctorId, long consultationId)
    {
        if (!await IsAuthorizedDoctorOwnerAsync(doctorId)) return;

        if (long.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId))
        {
            await _consultationRepo.CancelOrDeclineConsultationAsync(
                consultationId,
                callerUserId,
                "Declined",
                Context.User?.IsInRole("Admin") == true);
        }

        string key = NormalizeKey(doctorId.ToString());
        await Clients.Group($"Stream_{key}").SendAsync("ConsultationDeclined", new
        {
            doctorId,
            consultationId,
            message = "Doctor is currently occupied with stream advisory and unable to take private 1-to-1 calls right now."
        });
    }

    [Authorize]
    public async Task CancelConsultationRequest(long doctorId, long consultationId)
    {
        if (long.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId))
        {
            await _consultationRepo.CancelOrDeclineConsultationAsync(
                consultationId,
                callerUserId,
                "Cancelled",
                Context.User?.IsInRole("Admin") == true);
        }

        string key = NormalizeKey(doctorId.ToString());
        await Clients.Group($"Stream_{key}").SendAsync("ConsultationCancelled", new
        {
            doctorId,
            consultationId,
            message = "Patient has cancelled their consultation request."
        });
    }
}
