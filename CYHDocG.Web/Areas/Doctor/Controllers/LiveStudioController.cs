using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using CYHDocG.Repo.Interfaces;
using CYHDocG.Web.Hubs;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class LiveStudioController : Controller
{
    private readonly IDoctorRepository _doctorRepo;
    private readonly ILiveKitTokenService _liveKitTokenService;
    private readonly IHubContext<LiveReelHub> _hubContext;

    public LiveStudioController(
        IDoctorRepository doctorRepo, 
        ILiveKitTokenService liveKitTokenService,
        IHubContext<LiveReelHub> hubContext)
    {
        _doctorRepo = doctorRepo;
        _liveKitTokenService = liveKitTokenService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);

        if (metrics == null || !metrics.IsApproved)
        {
            TempData["ErrorMessage"] = "Your doctor account is pending Admin verification. Go Live Studio is currently locked.";
            return RedirectToAction("Index", "Dashboard");
        }
        
        string doctorName = metrics?.DoctorName ?? (User.Identity?.Name ?? "Doctor Specialist");
        long docId = metrics?.DoctorId > 0 ? metrics.DoctorId : userId;
        string roomName = $"stream_doc_{docId}";
        string token = _liveKitTokenService.GenerateAccessToken(roomName, $"doc_{userId}", doctorName, canPublish: true, canSubscribe: true);
        string serverUrl = _liveKitTokenService.GetServerUrl();

        ViewBag.RoomName = roomName;
        ViewBag.Token = token;
        ViewBag.ServerUrl = serverUrl;
        ViewBag.DoctorId = docId;
        ViewBag.DoctorName = doctorName;

        try
        {
            var comments = (await _doctorRepo.GetRecentCommentsAsync(docId)).Reverse().ToList();
            ViewBag.RecentComments = comments;
        }
        catch
        {
            ViewBag.RecentComments = new List<CYHDocG.Repo.DTOs.LiveCommentDto>();
        }

        return View(metrics);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLive([FromBody] LiveStatusRequest request)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);
        if (metrics == null || !metrics.IsApproved)
        {
            return BadRequest(new { success = false, message = "Doctor account is pending Admin approval." });
        }

        long docId = metrics?.DoctorId > 0 ? metrics.DoctorId : userId;
        string doctorName = metrics?.DoctorName ?? (User.Identity?.Name ?? "Doctor Specialist");

        var result = await _doctorRepo.SetDoctorLiveStatusAsync(docId, request.IsLive, request.StreamUrl);

        // Notify all viewers and reels feed of doctor's live state change in real time
        await _hubContext.Clients.All.SendAsync("DoctorLiveStatusChanged", new
        {
            doctorId = docId,
            isLive = request.IsLive,
            doctorName = doctorName,
            streamUrl = request.StreamUrl ?? ""
        });

        return Ok(new { success = result.Success, message = result.Message });
    }
}

public class LiveStatusRequest
{
    public bool IsLive { get; set; }
    public string? StreamUrl { get; set; }
}
