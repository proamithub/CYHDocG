using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorRepository _doctorRepo;

    public DoctorsController(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    [HttpGet("reels")]
    public async Task<IActionResult> GetReels([FromQuery] long? currentUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var reels = await _doctorRepo.GetReelsFeedAsync(currentUserId, page, pageSize);
        return Ok(reels);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var results = await _doctorRepo.SearchDoctorsAsync(query ?? "");
        return Ok(results);
    }

    [HttpPost("toggle-follow")]
    public async Task<IActionResult> ToggleFollow([FromQuery] long doctorId, [FromQuery] long userId)
    {
        var result = await _doctorRepo.ToggleFollowAsync(doctorId, userId);
        return Ok(result);
    }

    [HttpPost("toggle-like")]
    public async Task<IActionResult> ToggleLike([FromQuery] long streamId, [FromQuery] long userId)
    {
        var result = await _doctorRepo.ToggleStreamLikeAsync(streamId, userId);
        return Ok(result);
    }
}
