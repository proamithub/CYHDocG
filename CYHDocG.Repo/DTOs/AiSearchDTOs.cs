namespace CYHDocG.Repo.DTOs;

public class AiDiagnosisResultDto
{
    public string QueryText { get; set; } = string.Empty;
    public string IdentifiedDepartment { get; set; } = "General Medicine";
    public string SpecialtyNeeded { get; set; } = "General Physician";
    public string Severity { get; set; } = "Moderate"; // Low, Moderate, High, Emergency
    public bool IsEmergency { get; set; } = false;
    public bool IsMedical { get; set; } = true;
    public List<string> MatchedKeywords { get; set; } = new();
    public string AiAdviceEn { get; set; } = string.Empty;
    public string AiAdvice { get => AiAdviceEn; set => AiAdviceEn = value; }
    public List<DoctorSearchDto> RecommendedDoctors { get; set; } = new();
    public List<DoctorReelDto> LiveDoctors { get; set; } = new();
}
