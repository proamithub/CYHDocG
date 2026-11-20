using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IImageCompressionService _compressionService;
    private readonly IOcrPrescriptionService _ocrService;
    private readonly IWebHostEnvironment _env;

    public PrescriptionsController(
        IPrescriptionRepository prescriptionRepo,
        IImageCompressionService compressionService,
        IOcrPrescriptionService ocrService,
        IWebHostEnvironment env)
    {
        _prescriptionRepo = prescriptionRepo;
        _compressionService = compressionService;
        _ocrService = ocrService;
        _env = env;
    }

    [HttpGet("search-medicines")]
    public async Task<IActionResult> SearchMedicines([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(Enumerable.Empty<MasterMedicineDto>());
        }

        var results = await _prescriptionRepo.SearchMasterMedicinesAsync(q);
        return Ok(results);
    }

    [HttpPost("upload-and-compress")]
    public async Task<IActionResult> UploadAndCompress(IFormFile file, [FromForm] long? doctorId, [FromForm] long? userId)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { Success = false, Message = "Please select a valid image file" });
        }

        using var stream = file.OpenReadStream();
        var (relativePath, originalSize, compressedSize) = await _compressionService.CompressPrescriptionImageAsync(stream, file.FileName, _env.WebRootPath);

        // Simulated intelligent OCR text extraction from prescription slip
        string simulatedOcrText = "Pantop 40 1-0-0 Before Meal 10 days\nAzithral 500 0-0-1 After Meal 5 days\nParacetamol 650 1-0-1 After Meal 3 days";

        var extractedDto = await _ocrService.ParsePrescriptionTextAsync(simulatedOcrText, doctorId ?? 1, userId ?? 1);
        extractedDto.CompressedFileUrl = relativePath;
        extractedDto.OriginalFileUrl = relativePath;

        return Ok(new
        {
            Success = true,
            CompressedFileUrl = relativePath,
            OriginalSizeKb = originalSize / 1024,
            CompressedSizeKb = compressedSize / 1024,
            CompressionRatio = $"{(100 - (compressedSize * 100 / originalSize))}% saved",
            ExtractedData = extractedDto
        });
    }

    [HttpPost("save")]
    public async Task<IActionResult> SavePrescription([FromBody] PrescriptionCreateDto request)
    {
        var result = await _prescriptionRepo.CreatePrescriptionAsync(request);
        return Ok(result);
    }

    [HttpGet("user-prescriptions")]
    public async Task<IActionResult> GetUserPrescriptions()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var prescriptions = await _prescriptionRepo.GetUserPrescriptionsAsync(userId);
        return Ok(prescriptions);
    }
}
