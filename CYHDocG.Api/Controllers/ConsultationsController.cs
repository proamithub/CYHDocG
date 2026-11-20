using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsultationsController : ControllerBase
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly ILiveKitTokenService _liveKitTokenService;
    private readonly IConfiguration _config;

    public ConsultationsController(
        IConsultationRepository consultationRepo,
        ILiveKitTokenService liveKitTokenService,
        IConfiguration config)
    {
        _consultationRepo = consultationRepo;
        _liveKitTokenService = liveKitTokenService;
        _config = config;
    }

    [HttpPost("request")]
    public async Task<IActionResult> CreateRequest([FromBody] ConsultationRequestDto request)
    {
        var result = await _consultationRepo.CreateConsultationRequestAsync(request.UserId, request.DoctorId);
        return Ok(result);
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromQuery] long consultationId, [FromQuery] long doctorId)
    {
        var result = await _consultationRepo.AcceptConsultationAsync(consultationId, doctorId);
        return Ok(result);
    }

    [HttpPost("end")]
    public async Task<IActionResult> End([FromQuery] long consultationId, [FromQuery] long endedByUserId)
    {
        var result = await _consultationRepo.EndAndDeductWalletAsync(consultationId, endedByUserId);
        return Ok(result);
    }

    [HttpPost("video-token")]
    public IActionResult GetVideoToken([FromBody] VideoTokenRequestDto request, [FromQuery] string? participantIdentity = null, [FromQuery] string? participantName = null)
    {
        string identity = participantIdentity ?? $"user_{Guid.NewGuid().ToString("N")[..6]}";
        string name = participantName ?? "Participant";
        string roomName = !string.IsNullOrWhiteSpace(request.RoomName) ? request.RoomName : $"consultation_{request.ConsultationId}";

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
