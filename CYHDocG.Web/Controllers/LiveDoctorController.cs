using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

public class LiveDoctorController : Controller
{
    private readonly IDoctorRepository _doctorRepo;
    private readonly ILiveKitTokenService _liveKitTokenService;

    public LiveDoctorController(IDoctorRepository doctorRepo, ILiveKitTokenService liveKitTokenService)
    {
        _doctorRepo = doctorRepo;
        _liveKitTokenService = liveKitTokenService;
    }

    [HttpGet("/LiveDoctor")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index([FromQuery] string? q)
    {
        long? currentUserId = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdClaim, out long uId))
        {
            currentUserId = uId;
        }

        var reels = (await _doctorRepo.GetReelsFeedAsync(currentUserId, pageNumber: 1, pageSize: 15)).ToList();

        // Strictly show ONLY doctors who are currently live
        reels = reels.Where(r => r.IsLive).ToList();

        // If a search query is passed, filter doctors
        if (!string.IsNullOrWhiteSpace(q))
        {
            reels = reels.Where(r => r.DoctorName.Contains(q, StringComparison.OrdinalIgnoreCase) 
                                 || r.Specialty.Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || r.Department.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated == true;
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.ServerUrl = _liveKitTokenService.GetServerUrl();

        return View(reels);
    }

    [HttpGet("/api/livedoctor/subscriber-token")]
    public IActionResult GetSubscriberToken([FromQuery] long doctorId)
    {
        string roomName = $"stream_doc_{doctorId}";
        string participantId = User.Identity?.IsAuthenticated == true
            ? $"user_{User.FindFirstValue(ClaimTypes.NameIdentifier)}"
            : $"guest_{Guid.NewGuid().ToString("N")[..8]}";
        string participantName = User.Identity?.Name ?? "ReelViewer";

        try
        {
            string token = _liveKitTokenService.GenerateAccessToken(
                roomName,
                participantId,
                participantName,
                canPublish: false,
                canSubscribe: true);

            return Ok(new
            {
                success = true,
                roomName,
                serverUrl = _liveKitTokenService.GetServerUrl(),
                token
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("/api/livedoctor/toggle-follow")]
    public async Task<IActionResult> ToggleFollow([FromBody] long doctorId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { Success = false, Message = "Login required" });
        }

        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var result = await _doctorRepo.ToggleFollowAsync(doctorId, userId);
        return Ok(result);
    }

    [HttpPost("/api/livedoctor/toggle-like")]
    public async Task<IActionResult> ToggleLike([FromBody] long streamId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { Success = false, Message = "Login required" });
        }

        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var result = await _doctorRepo.ToggleStreamLikeAsync(streamId, userId);
        return Ok(result);
    }
}
