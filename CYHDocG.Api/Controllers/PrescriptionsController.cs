using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionRepository _prescriptionRepo;

    public PrescriptionsController(IPrescriptionRepository prescriptionRepo)
    {
        _prescriptionRepo = prescriptionRepo;
    }

    [HttpGet("medicines/search")]
    public async Task<IActionResult> SearchMedicines([FromQuery] string q)
    {
        var results = await _prescriptionRepo.SearchMasterMedicinesAsync(q ?? "");
        return Ok(results);
    }

    [HttpPost("save")]
    public async Task<IActionResult> SavePrescription([FromBody] PrescriptionCreateDto request)
    {
        var result = await _prescriptionRepo.CreatePrescriptionAsync(request);
        return Ok(result);
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserPrescriptions(long userId)
    {
        var prescriptions = await _prescriptionRepo.GetUserPrescriptionsAsync(userId);
        return Ok(prescriptions);
    }
}
