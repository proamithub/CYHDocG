using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

public class SearchController : Controller
{
    private readonly IAiSearchService _aiSearchService;
    private readonly IDoctorRepository _doctorRepo;

    public SearchController(IAiSearchService aiSearchService, IDoctorRepository doctorRepo)
    {
        _aiSearchService = aiSearchService;
        _doctorRepo = doctorRepo;
    }

    [HttpGet("/Search")]
    public async Task<IActionResult> Index([FromQuery] string? q)
    {
        var diagnosis = await _aiSearchService.AnalyzeSymptomAsync(q ?? "");
        return View(diagnosis);
    }

    [HttpGet("/api/search/suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(Enumerable.Empty<object>());
        }

        var results = await _doctorRepo.SearchDoctorsAsync(q);
        return Ok(results);
    }

    [HttpPost("/api/search/ai-analyze")]
    [HttpPost("/Search/Triage")]
    public async Task<IActionResult> Analyze([FromBody] System.Text.Json.JsonElement body)
    {
        string query = "";
        List<CYHDocG.Repo.DTOs.ChatTurnDto> history = new();

        if (body.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            query = body.GetString() ?? "";
        }
        else if (body.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (body.TryGetProperty("query", out var qProp)) query = qProp.GetString() ?? "";
            else if (body.TryGetProperty("message", out var mProp)) query = mProp.GetString() ?? "";

            if (body.TryGetProperty("history", out var hProp) && hProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in hProp.EnumerateArray())
                {
                    string role = item.TryGetProperty("role", out var r) ? (r.GetString() ?? "user") : "user";
                    string content = item.TryGetProperty("content", out var c) ? (c.GetString() ?? "") : "";
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        history.Add(new CYHDocG.Repo.DTOs.ChatTurnDto { Role = role, Content = content });
                    }
                }
            }
        }

        var result = await _aiSearchService.AnalyzeSymptomAsync(query, history);
        return Ok(result);
    }
}
