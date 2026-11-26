using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConsultationsController : ControllerBase
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IDoctorRepository _doctorRepo;
    private readonly ILiveKitTokenService _liveKitTokenService;

    public ConsultationsController(
        IConsultationRepository consultationRepo,
        IDoctorRepository doctorRepo,
        ILiveKitTokenService liveKitTokenService)
    {
        _consultationRepo = consultationRepo;
        _doctorRepo = doctorRepo;
        _liveKitTokenService = liveKitTokenService;
    }

    [HttpPost("request")]
    public async Task<IActionResult> CreateRequest([FromBody] ConsultationRequestDto request)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        var result = await _consultationRepo.CreateConsultationRequestAsync(callerUserId, request.DoctorId);
        return Ok(result);
    }

    [HttpPost("accept")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> Accept([FromQuery] long consultationId, [FromQuery] long doctorId)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId);
        bool isAdmin = User.IsInRole("Admin");

        long? doctorIdClaim = long.TryParse(User.FindFirstValue("DoctorId"), out long dClaim) ? dClaim : null;
        if (!isAdmin && !await _consultationRepo.IsDoctorAssignedAsync(consultationId, callerUserId, doctorIdClaim))
        {
            return Forbid();
        }

        long effectiveDoctorId = doctorIdClaim ?? (doctorId > 0 ? doctorId : callerUserId);
        if (effectiveDoctorId == callerUserId && !isAdmin)
        {
            var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(callerUserId);
            if (metrics?.DoctorId > 0) effectiveDoctorId = metrics.DoctorId;
        }

        var result = await _consultationRepo.AcceptConsultationAsync(consultationId, effectiveDoctorId);
        return Ok(result);
    }

    [HttpPost("end")]
    public async Task<IActionResult> End([FromQuery] long consultationId, [FromQuery] long endedByUserId)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        string role = User.FindFirstValue(ClaimTypes.Role) ?? "User";
        if (!await _consultationRepo.IsParticipantAsync(consultationId, callerUserId, role))
        {
            return Forbid();
        }

        var result = await _consultationRepo.EndAndDeductWalletAsync(consultationId, callerUserId);
        return Ok(result);
    }

    [HttpPost("video-token")]
    public async Task<IActionResult> GetVideoToken([FromBody] VideoTokenRequestDto request, [FromQuery] string? participantIdentity = null, [FromQuery] string? participantName = null)
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
        string name = User.FindFirstValue(ClaimTypes.Name) ?? (role == "Doctor" ? "Doctor" : "Patient");
        string roomName = $"consultation_{request.ConsultationId}";

        string token = _liveKitTokenService.GenerateAccessToken(roomName, identity, name);
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
}
