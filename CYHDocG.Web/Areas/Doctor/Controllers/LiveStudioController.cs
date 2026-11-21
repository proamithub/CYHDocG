using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class LiveStudioController : Controller
{
    private readonly IDoctorRepository _doctorRepo;
    private readonly ILiveKitTokenService _liveKitTokenService;

    public LiveStudioController(IDoctorRepository doctorRepo, ILiveKitTokenService liveKitTokenService)
    {
        _doctorRepo = doctorRepo;
        _liveKitTokenService = liveKitTokenService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);
        
        string doctorName = User.Identity?.Name ?? "Doctor Specialist";
        string roomName = $"stream_doc_{metrics?.DoctorId ?? userId}";
        string token = _liveKitTokenService.GenerateAccessToken(roomName, $"doc_{userId}", doctorName);
        string serverUrl = _liveKitTokenService.GetServerUrl();

        ViewBag.RoomName = roomName;
        ViewBag.Token = token;
        ViewBag.ServerUrl = serverUrl;
        ViewBag.DoctorId = metrics?.DoctorId ?? userId;
        ViewBag.DoctorName = doctorName;

        return View(metrics);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLive([FromBody] LiveStatusRequest request)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);
        long docId = metrics?.DoctorId ?? userId;

        var result = await _doctorRepo.SetDoctorLiveStatusAsync(docId, request.IsLive, request.StreamUrl);
        return Ok(new { success = result.Success, message = result.Message });
    }
}

public class LiveStatusRequest
{
    public bool IsLive { get; set; }
    public string? StreamUrl { get; set; }
}
