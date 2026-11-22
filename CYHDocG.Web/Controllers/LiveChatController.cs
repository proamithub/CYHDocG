using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;
using CYHDocG.Web.Hubs;

namespace CYHDocG.Web.Controllers;

public class LiveChatController : Controller
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IDoctorRepository _doctorRepo;
    private readonly IAiSearchService _aiSearchService;
    private readonly ILiveKitTokenService _liveKitTokenService;
    private readonly IConfiguration _config;
    private readonly IHubContext<LiveReelHub> _liveReelHub;
    private readonly IHubContext<ConsultationHub> _consultationHub;

    public LiveChatController(
        IConsultationRepository consultationRepo, 
        IDoctorRepository doctorRepo, 
        IAiSearchService aiSearchService,
        ILiveKitTokenService liveKitTokenService,
        IConfiguration config,
        IHubContext<LiveReelHub> liveReelHub,
        IHubContext<ConsultationHub> consultationHub)
    {
        _consultationRepo = consultationRepo;
        _doctorRepo = doctorRepo;
        _aiSearchService = aiSearchService;
        _liveKitTokenService = liveKitTokenService;
        _config = config;
        _liveReelHub = liveReelHub;
        _consultationHub = consultationHub;
    }

    [HttpPost("/api/consultation/video-token")]
    public IActionResult GetVideoToken([FromBody] VideoTokenRequestDto request)
    {
        string identity = User.Identity?.Name ?? $"user_{Guid.NewGuid().ToString("N")[..6]}";
        string participantName = User.Identity?.Name ?? "Patient";
        string roomName = !string.IsNullOrWhiteSpace(request.RoomName) ? request.RoomName : $"consultation_{request.ConsultationId}";

        string token = _liveKitTokenService.GenerateAccessToken(roomName, identity, participantName);
        string serverUrl = _liveKitTokenService.GetServerUrl();

        return Ok(new
        {
            success = true,
            token = token,
            serverUrl = serverUrl,
            roomName = roomName,
            participantIdentity = identity
        });
    }

    [HttpGet("/LiveChat")]
    public async Task<IActionResult> Index([FromQuery] long? doctorId)
    {
        long? currentUserId = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdClaim, out long uId))
        {
            currentUserId = uId;
        }

        ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated == true;
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.CurrentUserName = User.Identity?.Name ?? "Guest";
        ViewBag.DoctorId = doctorId;

        // Fetch live doctors who can accept chats
        var liveDoctors = await _doctorRepo.SearchDoctorsAsync("");
        return View(liveDoctors.ToList());
    }

    [HttpPost("/api/consultation/request")]
    public async Task<IActionResult> CreateRequest([FromBody] ConsultationRequestDto request)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { Success = false, Message = "Please sign in to request a doctor consultation." });
        }

        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        request.UserId = userId;

        var result = await _consultationRepo.CreateConsultationRequestAsync(request.UserId, request.DoctorId);
        if (result.Success && result.Data > 0)
        {
            string patientName = User.Identity?.Name ?? "Patient";
            // Notify doctor in real time on Doctor Live Studio screen
            await _liveReelHub.Clients.Group($"Stream_{request.DoctorId}").SendAsync("ReceiveIncomingConsultationRequest", new
            {
                consultationId = result.Data,
                patientId = userId,
                patientName = patientName,
                doctorId = request.DoctorId,
                requestedAt = DateTime.UtcNow
            });
        }

        return Ok(result);
    }

    [HttpPost("/api/consultation/accept")]
    public async Task<IActionResult> AcceptConsultation([FromBody] long consultationId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Doctor" && role != "Admin")
        {
            return Forbid();
        }

        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long currentUserId);
        var doctorIdClaim = User.FindFirstValue("DoctorId");
        long.TryParse(doctorIdClaim, out long doctorId);

        if (doctorId == 0)
        {
            var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(currentUserId);
            if (metrics?.DoctorId > 0) doctorId = metrics.DoctorId;
            else doctorId = currentUserId;
        }

        var result = await _consultationRepo.AcceptConsultationAsync(consultationId, doctorId);
        if (result.Success)
        {
            DateTime startedAt = result.Data;

            // Broadcast TimerStarted to Consultation room
            await _consultationHub.Clients.Group($"Consultation_{consultationId}").SendAsync("TimerStarted", new
            {
                startedAt = startedAt,
                consultationId = consultationId,
                doctorId = doctorId
            });

            // Also broadcast to LiveReelHub for direct handshake if patient requested from reel
            await _liveReelHub.Clients.Group($"Stream_{doctorId}").SendAsync("ConsultationAccepted", new
            {
                consultationId = consultationId,
                doctorId = doctorId,
                startedAt = startedAt
            });
        }

        return Ok(result);
    }

    [HttpPost("/api/consultation/end")]
    public async Task<IActionResult> EndConsultation([FromBody] long consultationId)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var result = await _consultationRepo.EndAndDeductWalletAsync(consultationId, userId);
        return Ok(result);
    }

    [HttpPost("/api/consultation/ai-chat")]
    public async Task<IActionResult> AiChat([FromBody] string query)
    {
        var diagnosis = await _aiSearchService.AnalyzeSymptomAsync(query);
        
        string reply = $"Based on your symptoms, this aligns with **{diagnosis.IdentifiedDepartment}** (Severity: {diagnosis.Severity}).\n\n{diagnosis.AiAdviceEn}\n\n*Would you like to connect directly with a certified specialist?*";

        return Ok(new
        {
            Reply = reply,
            Department = diagnosis.IdentifiedDepartment,
            Severity = diagnosis.Severity,
            IsEmergency = diagnosis.IsEmergency,
            DoctorsAvailable = diagnosis.RecommendedDoctors.Count
        });
    }
}
