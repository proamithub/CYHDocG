using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsultationsController : ControllerBase
{
    private readonly IConsultationRepository _consultationRepo;

    public ConsultationsController(IConsultationRepository consultationRepo)
    {
        _consultationRepo = consultationRepo;
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
}
