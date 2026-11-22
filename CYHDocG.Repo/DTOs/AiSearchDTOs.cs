namespace CYHDocG.Repo.DTOs;

public class ChatTurnDto
{
    public string Role { get; set; } = "user"; // "user" or "model"
    public string Content { get; set; } = string.Empty;
}

public class AiDiagnosisResultDto
{
    public string QueryText { get; set; } = string.Empty;
    public string IdentifiedDepartment { get; set; } = "General Medicine";
    public string SpecialtyNeeded { get; set; } = "General Physician";
    public string Severity { get; set; } = "Moderate"; // Low, Moderate, High, Emergency
    public bool IsEmergency { get; set; } = false;
    public bool IsMedical { get; set; } = true;
    public bool IsNeedsMoreInfo { get; set; } = false;
    public List<string> FollowUpQuestions { get; set; } = new();
    public List<string> HomeRemedies { get; set; } = new();
    public List<string> MatchedKeywords { get; set; } = new();
    public string AiAdviceEn { get; set; } = string.Empty;
    public string AiAdvice { get => AiAdviceEn; set => AiAdviceEn = value; }
    public List<DoctorSearchDto> RecommendedDoctors { get; set; } = new();
    public List<DoctorReelDto> LiveDoctors { get; set; } = new();
}
