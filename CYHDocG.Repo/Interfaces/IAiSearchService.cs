using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IAiSearchService
{
    Task<AiDiagnosisResultDto> AnalyzeSymptomAsync(string query, List<ChatTurnDto>? history = null);
}
