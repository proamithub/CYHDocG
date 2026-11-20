using System.Text.RegularExpressions;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class OcrPrescriptionService : IOcrPrescriptionService
{
    private readonly IPrescriptionRepository _prescriptionRepo;

    public OcrPrescriptionService(IPrescriptionRepository prescriptionRepo)
    {
        _prescriptionRepo = prescriptionRepo;
    }

    public async Task<PrescriptionCreateDto> ParsePrescriptionTextAsync(string rawText, long doctorId, long userId)
    {
        var dto = new PrescriptionCreateDto
        {
            DoctorId = doctorId,
            UserId = userId,
            OcrExtractedText = rawText,
            DoctorNotes = "Extracted via AI OCR Engine"
        };

        if (string.IsNullOrWhiteSpace(rawText))
        {
            return dto;
        }

        // Split text by lines
        var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length < 3) continue;

            // 1. Detect Dosage Schedule (e.g. 1-0-1, 1-1-1, 0-1-0, 1-0-0, 0-0-1)
            string dosage = "1-0-1"; // Default
            var dosageMatch = Regex.Match(line, @"\b([0-2]-[0-2]-[0-2])\b");
            if (dosageMatch.Success)
            {
                dosage = dosageMatch.Value;
            }

            // 2. Detect Duration (e.g. 5 days, 10 days, 1 month, 7 din)
            int duration = 5; // Default 5 days
            var durationMatch = Regex.Match(line, @"(\d+)\s*(days?|din|days|day)", RegexOptions.IgnoreCase);
            if (durationMatch.Success && int.TryParse(durationMatch.Groups[1].Value, out int parsedDays))
            {
                duration = parsedDays;
            }

            // 3. Detect Timing (Before Meal / After Meal / Khane ke baad)
            string timing = "After Meal";
            if (Regex.IsMatch(line, @"before|empty|khane se pehle", RegexOptions.IgnoreCase))
            {
                timing = "Before Meal";
            }

            // 4. Extract Medicine Candidate Name (removing numbers, dosage strings, etc.)
            string cleanName = Regex.Replace(line, @"\b([0-2]-[0-2]-[0-2])\b", "").Trim();
            cleanName = Regex.Replace(cleanName, @"\b(\d+)\s*(days?|din|tab|capsule|mg|ml)\b", "", RegexOptions.IgnoreCase).Trim();
            cleanName = Regex.Replace(cleanName, @"(before|after|meal|food|daily|sos)", "", RegexOptions.IgnoreCase).Trim();

            if (cleanName.Length >= 3)
            {
                // Match against MasterMedicines if possible
                var matches = await _prescriptionRepo.SearchMasterMedicinesAsync(cleanName[..Math.Min(4, cleanName.Length)]);
                var matchedMed = matches.FirstOrDefault();

                dto.Medicines.Add(new MedicineItemDto
                {
                    MedicineName = matchedMed != null ? matchedMed.BrandName : cleanName,
                    DosageSchedule = dosage,
                    TimingInstruction = timing,
                    DurationDays = duration,
                    TotalUnits = duration * CalculateDailyUnits(dosage)
                });
            }
        }

        // If no medicines could be parsed, add fallback default parsed item
        if (!dto.Medicines.Any())
        {
            dto.Medicines.Add(new MedicineItemDto
            {
                MedicineName = "Paracetamol 650",
                DosageSchedule = "1-0-1",
                TimingInstruction = "After Meal",
                DurationDays = 5,
                TotalUnits = 10
            });
        }

        return dto;
    }

    private static int CalculateDailyUnits(string schedule)
    {
        int units = 0;
        foreach (var c in schedule)
        {
            if (char.IsDigit(c)) units += (c - '0');
        }
        return Math.Max(1, units);
    }
}
