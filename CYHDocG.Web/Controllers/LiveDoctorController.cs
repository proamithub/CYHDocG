using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

public class LiveDoctorController : Controller
{
    private readonly IDoctorRepository _doctorRepo;

    public LiveDoctorController(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    [HttpGet("/LiveDoctor")]
    public async Task<IActionResult> Index([FromQuery] string? q)
    {
        long? currentUserId = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdClaim, out long uId))
        {
            currentUserId = uId;
        }

        var reels = (await _doctorRepo.GetReelsFeedAsync(currentUserId, pageNumber: 1, pageSize: 15)).ToList();

        // If a search query is passed, filter doctors
        if (!string.IsNullOrWhiteSpace(q))
        {
            reels = reels.Where(r => r.DoctorName.Contains(q, StringComparison.OrdinalIgnoreCase) 
                                 || r.Specialty.Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || r.Department.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated == true;
        ViewBag.CurrentUserId = currentUserId;

        return View(reels);
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
