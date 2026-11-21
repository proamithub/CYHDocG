using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class PrescriptionsController : Controller
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IDoctorRepository _doctorRepo;

    public PrescriptionsController(IPrescriptionRepository prescriptionRepo, IDoctorRepository doctorRepo)
    {
        _prescriptionRepo = prescriptionRepo;
        _doctorRepo = doctorRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] long? userId, [FromQuery] long? consultationId)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long currentUserId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(currentUserId);
        if (metrics == null || !metrics.IsApproved)
        {
            TempData["ErrorMessage"] = "Your doctor account is pending Admin verification. Prescription Creator is currently locked.";
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.TargetUserId = userId ?? 1;
        ViewBag.ConsultationId = consultationId;
        return View();
    }
}
