using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
    private readonly ISettingsRepository _settingsRepo;

    public SecurityController(ISettingsRepository settingsRepo)
    {
        _settingsRepo = settingsRepo;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSecurityStatus()
    {
        var setting = await _settingsRepo.GetSettingByKeyAsync("AntiInspectEnabled");
        bool isEnabled = setting == null || (setting.IsActive && setting.SettingValue == "1");
        return Ok(new { AntiInspectEnabled = isEnabled });
    }

    [HttpPost("log-event")]
    [AllowAnonymous]
    public async Task<IActionResult> LogSecurityIncident([FromBody] SecurityLogDto log)
    {
        log.UserId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long uid) ? uid : null;
        log.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _settingsRepo.LogSecurityEventAsync(log);
        return Ok(new { Success = true });
    }

    [HttpPost("toggle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleAntiInspect([FromBody] bool enable)
    {
        var result = await _settingsRepo.UpdateSettingAsync("AntiInspectEnabled", enable ? "1" : "0", true);
        return Ok(result);
    }
}
