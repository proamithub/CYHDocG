using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IDoctorRepository _doctorRepo;
    private readonly IImageCompressionService _compressionService;
    private readonly IOcrPrescriptionService _ocrService;
    private readonly IWebHostEnvironment _env;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private const long MaxUploadBytes = 10 * 1024 * 1024; // 10 MB

    public PrescriptionsController(
        IPrescriptionRepository prescriptionRepo,
        IDoctorRepository doctorRepo,
        IImageCompressionService compressionService,
        IOcrPrescriptionService ocrService,
        IWebHostEnvironment env)
    {
        _prescriptionRepo = prescriptionRepo;
        _doctorRepo = doctorRepo;
        _compressionService = compressionService;
        _ocrService = ocrService;
        _env = env;
    }

    [HttpGet("search-medicines")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> SearchMedicines([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(Enumerable.Empty<MasterMedicineDto>());
        }

        var results = await _prescriptionRepo.SearchMasterMedicinesAsync(q);
        return Ok(results);
    }

    [HttpGet("search-patients")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> SearchPatients([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Count(char.IsDigit) < 10)
        {
            return Ok(Enumerable.Empty<PatientSummaryDto>());
        }

        var results = await _prescriptionRepo.SearchPatientsForDoctorAsync(q);
        return Ok(results);
    }

    [HttpPost("upload-and-compress")]
    [Authorize(Roles = "Doctor,Admin")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> UploadAndCompress(IFormFile file, [FromForm] long? doctorId, [FromForm] long? userId)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { Success = false, Message = "Please select a valid image or PDF file" });
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new { Success = false, Message = "File size must not exceed 10 MB" });
        }

        string ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return BadRequest(new { Success = false, Message = "Only JPG, PNG, WebP, or PDF prescription files are allowed" });
        }

        long effectiveDoctorId = await ResolveAuthorizedDoctorIdAsync(doctorId);
        if (effectiveDoctorId <= 0)
        {
            return Forbid();
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        string relativePath;
        long originalSize = fileBytes.Length;
        long compressedSize;
        string mimeType;

        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "prescriptions");
            Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = $"rx_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}.pdf";
            string targetFilePath = Path.Combine(uploadsFolder, uniqueFileName);
            await System.IO.File.WriteAllBytesAsync(targetFilePath, fileBytes);
            relativePath = $"/uploads/prescriptions/{uniqueFileName}";
            compressedSize = originalSize;
            mimeType = "application/pdf";
        }
        else
        {
            using var stream = new MemoryStream(fileBytes);
            (relativePath, originalSize, compressedSize) = await _compressionService.CompressPrescriptionImageAsync(stream, file.FileName, _env.WebRootPath);
            mimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;
        }

        var extractedDto = await _ocrService.ExtractAndParsePrescriptionAsync(fileBytes, mimeType, effectiveDoctorId, userId ?? 0);
        extractedDto.CompressedFileUrl = relativePath;
        extractedDto.OriginalFileUrl = relativePath;

        long savedPct = originalSize > 0 ? Math.Max(0, 100 - (compressedSize * 100 / originalSize)) : 0;

        return Ok(new
        {
            Success = true,
            CompressedFileUrl = relativePath,
            OriginalSizeKb = originalSize / 1024,
            CompressedSizeKb = compressedSize / 1024,
            CompressionRatio = mimeType == "application/pdf" ? "PDF stored" : $"{savedPct}% saved",
            ExtractedData = extractedDto
        });
    }

    [HttpPost("save")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> SavePrescription([FromBody] PrescriptionCreateDto request)
    {
        long effectiveDoctorId = await ResolveAuthorizedDoctorIdAsync(request.DoctorId);
        if (effectiveDoctorId <= 0)
        {
            return Forbid();
        }

        if (request.UserId <= 0)
        {
            return BadRequest(new { Success = false, Message = "Please select a patient before issuing a prescription." });
        }

        request.DoctorId = effectiveDoctorId;
        var result = await _prescriptionRepo.CreatePrescriptionAsync(request);
        return Ok(result);
    }

    [HttpGet("doctor-history")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> GetDoctorPrescriptionHistory(
        [FromQuery] long? doctorId,
        [FromQuery] long? userId,
        [FromQuery] long? consultationId)
    {
        long effectiveDoctorId = await ResolveAuthorizedDoctorIdAsync(doctorId);
        if (effectiveDoctorId <= 0 && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var history = await _prescriptionRepo.GetIssuedPrescriptionsForDoctorAsync(effectiveDoctorId, userId, consultationId);
        return Ok(history);
    }

    [HttpGet("user-prescriptions")]
    public async Task<IActionResult> GetUserPrescriptions()
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId) || userId <= 0)
        {
            return Unauthorized();
        }

        var prescriptions = await _prescriptionRepo.GetUserPrescriptionsAsync(userId);
        return Ok(prescriptions);
    }

    [HttpPost("reminders/{reminderId:long}/status")]
    public async Task<IActionResult> UpdateReminderStatus(long reminderId, [FromBody] string status)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId) || userId <= 0)
        {
            return Unauthorized();
        }

        var result = await _prescriptionRepo.MarkReminderStatusAsync(reminderId, userId, status);
        return Ok(result);
    }

    private async Task<long> ResolveAuthorizedDoctorIdAsync(long? requestedDoctorId)
    {
        if (User.IsInRole("Admin"))
        {
            return requestedDoctorId ?? 0;
        }

        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return 0;
        }

        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(callerUserId);
        if (metrics == null || !metrics.IsApproved || metrics.DoctorId <= 0)
        {
            return 0;
        }

        return metrics.DoctorId;
    }
}
