using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IOcrPrescriptionService
{
    Task<PrescriptionCreateDto> ParsePrescriptionTextAsync(string rawText, long doctorId, long userId);
    Task<PrescriptionCreateDto> ExtractAndParsePrescriptionAsync(byte[] imageBytes, string mimeType, long doctorId, long userId);
}
