namespace CYHDocG.Repo.DTOs;

public class AiDiagnosisResultDto
{
    public string QueryText { get; set; } = string.Empty;
    public string IdentifiedDepartment { get; set; } = "General Medicine";
    public string SpecialtyNeeded { get; set; } = "General Physician";
    public string Severity { get; set; } = "Moderate"; // Low, Moderate, High, Emergency
    public bool IsEmergency { get; set; } = false;
    public List<string> MatchedKeywords { get; set; } = new();
    public string AiAdviceEn { get; set; } = string.Empty;
    public string AiAdviceHi { get; set; } = string.Empty;
    public string AiAdviceHinglish { get; set; } = string.Empty;
    public List<DoctorSearchDto> RecommendedDoctors { get; set; } = new();
}
