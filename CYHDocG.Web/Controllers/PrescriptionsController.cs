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

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        using var stream = new MemoryStream(fileBytes);
        var (relativePath, originalSize, compressedSize) = await _compressionService.CompressPrescriptionImageAsync(stream, file.FileName, _env.WebRootPath);

        var extractedDto = await _ocrService.ExtractAndParsePrescriptionAsync(fileBytes, file.ContentType, doctorId ?? 1, userId ?? 1);
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
