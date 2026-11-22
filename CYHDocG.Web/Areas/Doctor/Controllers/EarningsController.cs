using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class EarningsController : Controller
{
    private readonly IDoctorRepository _doctorRepo;

    public EarningsController(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);
        if (metrics == null || !metrics.IsApproved)
        {
            TempData["ErrorMessage"] = "Your doctor account is pending Admin verification. Earnings & Wallet is currently locked.";
            return RedirectToAction("Index", "Dashboard");
        }

        var model = await _doctorRepo.GetDoctorEarningsSummaryAsync(userId);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> RequestPayout([FromBody] DoctorPayoutRequestDto dto)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        if (dto == null || dto.Amount <= 0)
        {
            return BadRequest(new { success = false, message = "Please enter a valid payout amount." });
        }

        var result = await _doctorRepo.RequestDoctorPayoutAsync(userId, dto.Amount, dto.BankName, dto.AccountNumber, dto.IfscCode, dto.UpiId);
        return Ok(result);
    }
}
