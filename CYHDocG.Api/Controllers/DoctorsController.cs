using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
    [AllowAnonymous]
    public async Task<IActionResult> GetReels([FromQuery] long? currentUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        long? authenticatedUserId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long uid) ? uid : null;
        var reels = await _doctorRepo.GetReelsFeedAsync(authenticatedUserId ?? currentUserId, page, pageSize);
        return Ok(reels);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var results = await _doctorRepo.SearchDoctorsAsync(query ?? "");
        return Ok(results);
    }

    [HttpPost("toggle-follow")]
    [Authorize]
    public async Task<IActionResult> ToggleFollow([FromQuery] long doctorId, [FromQuery] long userId)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        var result = await _doctorRepo.ToggleFollowAsync(doctorId, callerUserId);
        return Ok(result);
    }

    [HttpPost("toggle-like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike([FromQuery] long streamId, [FromQuery] long userId)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        var result = await _doctorRepo.ToggleStreamLikeAsync(streamId, callerUserId);
        return Ok(result);
    }
}
