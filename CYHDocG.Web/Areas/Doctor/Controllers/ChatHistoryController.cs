using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class ChatHistoryController : Controller
{
    private readonly IDoctorRepository _doctorRepo;

    public ChatHistoryController(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] long? sessionId,
        [FromQuery] string? range = "all",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? search = null,
        [FromQuery] string? chatSearch = null)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);

        if (metrics == null || !metrics.IsApproved)
        {
            TempData["ErrorMessage"] = "Your doctor account is pending Admin verification. Chat History is currently locked.";
            return RedirectToAction("Index", "Dashboard");
        }

        long docId = metrics?.DoctorId > 0 ? metrics.DoctorId : userId;
        string doctorName = metrics?.DoctorName ?? (User.Identity?.Name ?? "Doctor");

        // Parse date range preset
        DateTime? filterStart = startDate;
        DateTime? filterEnd = endDate;

        if (!string.IsNullOrWhiteSpace(range) && range.ToLower() != "custom")
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            switch (range.ToLower())
            {
                case "today":
                    filterStart = today;
                    filterEnd = today.AddDays(1).AddTicks(-1);
                    break;
                case "yesterday":
                    filterStart = today.AddDays(-1);
                    filterEnd = today.AddTicks(-1);
                    break;
                case "week":
                    filterStart = today.AddDays(-7);
                    filterEnd = today.AddDays(1).AddTicks(-1);
                    break;
                case "month":
                    filterStart = today.AddDays(-30);
                    filterEnd = today.AddDays(1).AddTicks(-1);
                    break;
                case "all":
                default:
                    filterStart = null;
                    filterEnd = null;
                    break;
            }
        }

        var sessions = (await _doctorRepo.GetDoctorLiveSessionsAsync(docId, filterStart, filterEnd, search)).ToList();

        LiveStreamSessionDto? selectedSession = null;
        if (sessionId.HasValue && sessionId.Value > 0)
        {
            selectedSession = sessions.FirstOrDefault(s => s.StreamId == sessionId.Value)
                              ?? await _doctorRepo.GetSessionDetailsAsync(sessionId.Value);
        }
        else if (sessions.Any())
        {
            // Default to session with comments or the most recent
            selectedSession = sessions.FirstOrDefault(s => s.TotalComments > 0) ?? sessions.First();
        }

        var comments = new List<LiveCommentDto>();
        if (selectedSession != null)
        {
            comments = (await _doctorRepo.GetSessionCommentsAsync(selectedSession.StreamId, chatSearch)).ToList();
        }

        var vm = new DoctorChatHistoryViewModel
        {
            DoctorId = docId,
            DoctorName = doctorName,
            Sessions = sessions,
            SelectedSession = selectedSession,
            Comments = comments,
            Range = range ?? "all",
            StartDate = startDate,
            EndDate = endDate,
            Search = search,
            ChatSearch = chatSearch
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Download([FromQuery] long sessionId, [FromQuery] string format = "csv")
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);
        if (metrics == null || !metrics.IsApproved)
        {
            return Unauthorized();
        }

        long docId = metrics?.DoctorId > 0 ? metrics.DoctorId : userId;
        string doctorName = metrics?.DoctorName ?? "Doctor";

        var session = await _doctorRepo.GetSessionDetailsAsync(sessionId);
        if (session == null || session.DoctorId != docId)
        {
            return NotFound("Live stream session not found or access denied.");
        }

        var comments = (await _doctorRepo.GetSessionCommentsAsync(sessionId)).ToList();
        string dateStr = (session.StartedAt ?? session.CreatedAt).ToString("yyyyMMdd_HHmm");

        if (format.Equals("txt", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================================");
            sb.AppendLine($" CYHDocGuru - LIVE PATIENT CHAT TRANSCRIPT");
            sb.AppendLine($" Doctor: Dr. {doctorName}");
            sb.AppendLine($" Session: {session.Title} (ID: #{session.StreamId})");
            sb.AppendLine($" Started: {(session.StartedAt ?? session.CreatedAt):dd MMM yyyy, hh:mm tt} UTC");
            sb.AppendLine($" Ended:   {(session.EndedAt.HasValue ? session.EndedAt.Value.ToString("dd MMM yyyy, hh:mm tt UTC") : (session.IsActive ? "Still Live" : "Ended"))}");
            sb.AppendLine($" Total Messages: {comments.Count}");
            sb.AppendLine("==================================================================");
            sb.AppendLine();

            if (!comments.Any())
            {
                sb.AppendLine("No patient messages recorded during this live session.");
            }
            else
            {
                foreach (var c in comments)
                {
                    var isDoc = c.UserName.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) 
                                || c.UserName.Equals(doctorName, StringComparison.OrdinalIgnoreCase);
                    var roleTag = isDoc ? "[DOCTOR]" : "[PATIENT]";
                    sb.AppendLine($"[{c.CreatedAt:hh:mm:ss tt}] {roleTag} {c.UserName}:");
                    sb.AppendLine($"  {c.Message}");
                    sb.AppendLine();
                }
            }

            var txtBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(txtBytes, "text/plain", $"LiveChat_Session_{sessionId}_{dateStr}.txt");
        }
        else
        {
            // CSV Export with UTF-8 BOM
            var sb = new StringBuilder();
            // UTF-8 BOM for Excel compatibility
            sb.Append('\uFEFF');
            sb.AppendLine("MessageId,TimestampUTC,SenderName,Role,Message");

            foreach (var c in comments)
            {
                var isDoc = c.UserName.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) 
                            || c.UserName.Equals(doctorName, StringComparison.OrdinalIgnoreCase);
                var role = isDoc ? "Doctor" : "Patient";
                string cleanMsg = EscapeCsv(c.Message);
                string cleanUser = EscapeCsv(c.UserName);

                sb.AppendLine($"{c.CommentId},{c.CreatedAt:yyyy-MM-dd HH:mm:ss},{cleanUser},{role},{cleanMsg}");
            }

            var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(csvBytes, "text/csv", $"LiveChat_Session_{sessionId}_{dateStr}.csv");
        }
    }

    private static string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";
        field = field.Replace("\"", "\"\"");
        return $"\"{field}\"";
    }
}

public class DoctorChatHistoryViewModel
{
    public long DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public List<LiveStreamSessionDto> Sessions { get; set; } = new();
    public LiveStreamSessionDto? SelectedSession { get; set; }
    public List<LiveCommentDto> Comments { get; set; } = new();
    public string Range { get; set; } = "all";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Search { get; set; }
    public string? ChatSearch { get; set; }
}
