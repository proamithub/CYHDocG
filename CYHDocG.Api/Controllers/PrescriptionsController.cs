using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IDoctorRepository _doctorRepo;

    public PrescriptionsController(IPrescriptionRepository prescriptionRepo, IDoctorRepository doctorRepo)
    {
        _prescriptionRepo = prescriptionRepo;
        _doctorRepo = doctorRepo;
    }

    [HttpGet("medicines/search")]
    public async Task<IActionResult> SearchMedicines([FromQuery] string q)
    {
        var results = await _prescriptionRepo.SearchMasterMedicinesAsync(q ?? "");
        return Ok(results);
    }

    [HttpPost("save")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> SavePrescription([FromBody] PrescriptionCreateDto request)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId);
        if (!User.IsInRole("Admin"))
        {
            var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(callerUserId);
            if (metrics == null || !metrics.IsApproved || metrics.DoctorId <= 0)
            {
                return Forbid();
            }
            request.DoctorId = metrics.DoctorId;
        }

        var result = await _prescriptionRepo.CreatePrescriptionAsync(request);
        return Ok(result);
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserPrescriptions(long userId)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        if (callerUserId != userId && !User.IsInRole("Doctor") && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var prescriptions = await _prescriptionRepo.GetUserPrescriptionsAsync(userId);
        return Ok(prescriptions);
    }
}
