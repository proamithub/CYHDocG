namespace CYHDocG.Repo.DTOs;

public class MasterMedicineDto
{
    public long MasterMedicineId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string DosageForm { get; set; } = "Tablet";
    public string? Strength { get; set; }
    public string? Manufacturer { get; set; }
}

public class PrescriptionCreateDto
{
    public long? ConsultationId { get; set; }
    public long DoctorId { get; set; }
    public long UserId { get; set; }
    public string? OriginalFileUrl { get; set; }
    public string? CompressedFileUrl { get; set; }
    public string? OcrExtractedText { get; set; }
    public string? DoctorNotes { get; set; }
    public List<MedicineItemDto> Medicines { get; set; } = new();
}

public class MedicineItemDto
{
    public string MedicineName { get; set; } = string.Empty;
    public string DosageSchedule { get; set; } = "1-0-1"; // Morning - Afternoon - Night
    public string? TimingInstruction { get; set; } = "After Meal";
    public int DurationDays { get; set; } = 5;
    public int TotalUnits { get; set; } = 10;
}

public class PrescriptionHeaderDto
{
    public long PrescriptionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CompressedFileUrl { get; set; }
    public string? DoctorNotes { get; set; }
    public string Specialty { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public List<PrescriptionMedicineItemDto> Medicines { get; set; } = new();
}

public class PrescriptionMedicineItemDto
{
    public long PrescriptionMedicineId { get; set; }
    public long PrescriptionId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string DosageSchedule { get; set; } = string.Empty;
    public string? TimingInstruction { get; set; }
    public int DurationDays { get; set; }
    public DateTime CourseEndDate { get; set; }
    public DateTime RefillAlertDate { get; set; }
}

public class AppSettingDto
{
    public int SettingId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SecurityLogDto
{
    public long? UserId { get; set; }
    public string EventAction { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
