using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
    private readonly IHubContext<LiveReelHub> _liveReelHub;
    private readonly IHubContext<ConsultationHub> _consultationHub;

    public LiveChatController(
        IConsultationRepository consultationRepo,
        IDoctorRepository doctorRepo,
        IAiSearchService aiSearchService,
        ILiveKitTokenService liveKitTokenService,
        IHubContext<LiveReelHub> liveReelHub,
        IHubContext<ConsultationHub> consultationHub)
    {
        _consultationRepo = consultationRepo;
        _doctorRepo = doctorRepo;
        _aiSearchService = aiSearchService;
        _liveKitTokenService = liveKitTokenService;
        _liveReelHub = liveReelHub;
        _consultationHub = consultationHub;
    }

    [Authorize]
    [HttpPost("/api/consultation/video-token")]
    public async Task<IActionResult> GetVideoToken([FromBody] VideoTokenRequestDto request)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        string role = User.FindFirstValue(ClaimTypes.Role) ?? "User";
        if (request.ConsultationId <= 0 || !await _consultationRepo.IsParticipantAsync(request.ConsultationId, callerUserId, role))
        {
            return Forbid();
        }

        string identity = role == "Doctor" ? $"doc_{callerUserId}" : $"user_{callerUserId}";
        string participantName = role == "Doctor"
            ? $"Dr. {User.Identity?.Name ?? "Doctor"}"
            : (User.Identity?.Name ?? "Patient");
        string roomName = $"consultation_{request.ConsultationId}";

        string token = _liveKitTokenService.GenerateAccessToken(roomName, identity, participantName, canPublish: true, canSubscribe: true);
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
    public async Task<IActionResult> Index([FromQuery] long? doctorId, [FromQuery] long? consultationId)
    {
        long? currentUserId = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdClaim, out long uId))
        {
            currentUserId = uId;
        }

        string userRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";
        ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated == true;
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.CurrentUserName = User.Identity?.Name ?? "Guest";
        ViewBag.UserRole = userRole;
        ViewBag.DoctorId = doctorId;
        ViewBag.ConsultationId = consultationId;

        if (consultationId.HasValue && consultationId.Value > 0)
        {
            return RedirectToAction("Room", "ConsultationRoom", new { id = consultationId.Value });
        }

        var liveDoctors = await _doctorRepo.SearchDoctorsAsync("");
        return View(liveDoctors.ToList());
    }

    [Authorize]
    [HttpPost("/api/consultation/request")]
    public async Task<IActionResult> CreateRequest([FromBody] ConsultationRequestDto request)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId) || userId <= 0)
        {
            return Unauthorized(new { Success = false, Message = "Please sign in to request a doctor consultation." });
        }

        request.UserId = userId;

        var result = await _consultationRepo.CreateConsultationRequestAsync(request.UserId, request.DoctorId);
        if (result.Success && result.Data > 0)
        {
            string patientName = User.Identity?.Name ?? "Patient";
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

    [Authorize(Roles = "Doctor,Admin")]
    [HttpPost("/api/consultation/accept")]
    public async Task<IActionResult> AcceptConsultation([FromBody] long consultationId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long currentUserId);
        var doctorIdClaim = User.FindFirstValue("DoctorId");
        long.TryParse(doctorIdClaim, out long doctorId);

        if (role != "Admin" && !await _consultationRepo.IsDoctorAssignedAsync(consultationId, currentUserId, doctorId > 0 ? doctorId : null))
        {
            return Forbid();
        }

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

            await _consultationHub.Clients.Group($"Consultation_{consultationId}").SendAsync("TimerStarted", new
            {
                startedAt = startedAt,
                consultationId = consultationId,
                doctorId = doctorId
            });

            await _liveReelHub.Clients.Group($"Stream_{doctorId}").SendAsync("ConsultationAccepted", new
            {
                consultationId = consultationId,
                doctorId = doctorId,
                startedAt = startedAt
            });
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("/api/consultation/end")]
    public async Task<IActionResult> EndConsultation([FromBody] long consultationId)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId) || userId <= 0)
        {
            return Unauthorized();
        }

        string role = User.FindFirstValue(ClaimTypes.Role) ?? "User";
        if (!await _consultationRepo.IsParticipantAsync(consultationId, userId, role))
        {
            return Forbid();
        }

        var result = await _consultationRepo.EndAndDeductWalletAsync(consultationId, userId);
        if (result.Success && result.Data != null)
        {
            await _consultationHub.Clients.Group($"Consultation_{consultationId}").SendAsync("ConsultationEnded", result.Data);
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("/api/consultation/cancel")]
    public async Task<IActionResult> CancelConsultation([FromBody] long consultationId)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId) || userId <= 0)
        {
            return Unauthorized();
        }

        string role = User.FindFirstValue(ClaimTypes.Role) ?? "User";
        if (!await _consultationRepo.IsParticipantAsync(consultationId, userId, role))
        {
            return Forbid();
        }

        string targetStatus = role == "Doctor" ? "Declined" : "Cancelled";
        var result = await _consultationRepo.CancelOrDeclineConsultationAsync(consultationId, userId, targetStatus, role == "Admin");
        return Ok(result);
    }

    [HttpPost("/api/consultation/ai-chat")]
    public async Task<IActionResult> AiChat([FromBody] System.Text.Json.JsonElement body)
    {
        string query = "";
        List<CYHDocG.Repo.DTOs.ChatTurnDto> history = new();

        if (body.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            query = body.GetString() ?? "";
        }
        else if (body.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (body.TryGetProperty("query", out var qProp)) query = qProp.GetString() ?? "";
            else if (body.TryGetProperty("message", out var mProp)) query = mProp.GetString() ?? "";

            if (body.TryGetProperty("history", out var hProp) && hProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in hProp.EnumerateArray())
                {
                    string role = item.TryGetProperty("role", out var r) ? (r.GetString() ?? "user") : "user";
                    string content = item.TryGetProperty("content", out var c) ? (c.GetString() ?? "") : "";
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        history.Add(new CYHDocG.Repo.DTOs.ChatTurnDto { Role = role, Content = content });
                    }
                }
            }
        }

        var diagnosis = await _aiSearchService.AnalyzeSymptomAsync(query, history);
        return Ok(diagnosis);
    }
}
