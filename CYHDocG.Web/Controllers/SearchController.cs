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
    public async Task<IActionResult> Analyze([FromBody] string query)
    {
        var result = await _aiSearchService.AnalyzeSymptomAsync(query);
        return Ok(result);
    }
}
