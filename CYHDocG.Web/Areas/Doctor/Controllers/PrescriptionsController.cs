using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class PrescriptionsController : Controller
{
    private readonly IDoctorRepository _doctorRepo;
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IBaseRepository _baseRepo;

    public PrescriptionsController(
        IDoctorRepository doctorRepo,
        IPrescriptionRepository prescriptionRepo,
        IBaseRepository baseRepo)
    {
        _doctorRepo = doctorRepo;
        _prescriptionRepo = prescriptionRepo;
        _baseRepo = baseRepo;
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

        PatientSummaryDto? selectedPatient = null;
        if (userId.HasValue && userId.Value > 0)
        {
            using var conn = _baseRepo.CreateConnection();
            selectedPatient = await conn.QueryFirstOrDefaultAsync<PatientSummaryDto>(
                "SELECT UserId, FullName, Email, ISNULL(Phone, '') AS Phone FROM dbo.Users WHERE UserId = @UserId",
                new { UserId = userId.Value });
        }

        long? resolvedPatientId = selectedPatient?.UserId;
        var initialHistory = (await _prescriptionRepo.GetIssuedPrescriptionsForDoctorAsync(
            metrics.DoctorId,
            resolvedPatientId,
            consultationId)).ToList();

        ViewBag.DoctorId = metrics.DoctorId;
        ViewBag.TargetUserId = selectedPatient?.UserId ?? 0;
        ViewBag.TargetPatientName = selectedPatient?.FullName ?? "";
        ViewBag.TargetPatientContact = selectedPatient != null
            ? (!string.IsNullOrEmpty(selectedPatient.Phone) ? $"{selectedPatient.Phone} • {selectedPatient.Email}" : selectedPatient.Email)
            : "";
        ViewBag.ConsultationId = consultationId;
        ViewBag.InitialHistory = initialHistory;
        return View();
    }
}
