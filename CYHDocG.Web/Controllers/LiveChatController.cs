using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

public class LiveChatController : Controller
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IDoctorRepository _doctorRepo;
    private readonly IAiSearchService _aiSearchService;

    public LiveChatController(IConsultationRepository consultationRepo, IDoctorRepository doctorRepo, IAiSearchService aiSearchService)
    {
        _consultationRepo = consultationRepo;
        _doctorRepo = doctorRepo;
        _aiSearchService = aiSearchService;
    }

    [HttpGet("/LiveChat")]
    public async Task<IActionResult> Index([FromQuery] long? doctorId)
    {
        long? currentUserId = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdClaim, out long uId))
        {
            currentUserId = uId;
        }

        ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated == true;
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.CurrentUserName = User.Identity?.Name ?? "Guest";
        ViewBag.DoctorId = doctorId;

        // Fetch live doctors who can accept chats
        var liveDoctors = await _doctorRepo.SearchDoctorsAsync("");
        return View(liveDoctors.ToList());
    }

    [HttpPost("/api/consultation/request")]
    public async Task<IActionResult> CreateRequest([FromBody] ConsultationRequestDto request)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { Success = false, Message = "Please sign in to request a doctor consultation." });
        }

        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        request.UserId = userId;

        var result = await _consultationRepo.CreateConsultationRequestAsync(request.UserId, request.DoctorId);
        return Ok(result);
    }

    [HttpPost("/api/consultation/accept")]
    public async Task<IActionResult> AcceptConsultation([FromBody] long consultationId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Doctor" && role != "Admin")
        {
            return Forbid();
        }

        var doctorIdClaim = User.FindFirstValue("DoctorId");
        long.TryParse(doctorIdClaim, out long doctorId);

        var result = await _consultationRepo.AcceptConsultationAsync(consultationId, doctorId);
        return Ok(result);
    }

    [HttpPost("/api/consultation/end")]
    public async Task<IActionResult> EndConsultation([FromBody] long consultationId)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var result = await _consultationRepo.EndAndDeductWalletAsync(consultationId, userId);
        return Ok(result);
    }

    [HttpPost("/api/consultation/ai-chat")]
    public async Task<IActionResult> AiChat([FromBody] string query)
    {
        var diagnosis = await _aiSearchService.AnalyzeSymptomAsync(query);
        
        string reply = $"Based on your symptoms, this aligns with **{diagnosis.IdentifiedDepartment}** (Severity: {diagnosis.Severity}).\n\n{diagnosis.AiAdviceEn}\n\n*Would you like to connect directly with a certified specialist?*";

        return Ok(new
        {
            Reply = reply,
            Department = diagnosis.IdentifiedDepartment,
            Severity = diagnosis.Severity,
            IsEmergency = diagnosis.IsEmergency,
            DoctorsAvailable = diagnosis.RecommendedDoctors.Count
        });
    }
}
