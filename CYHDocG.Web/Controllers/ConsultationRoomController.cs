using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using CYHDocG.Repo.Interfaces;
using CYHDocG.Web.Hubs;

namespace CYHDocG.Web.Controllers;

[Authorize]
public class ConsultationRoomController : Controller
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IDoctorRepository _doctorRepo;
    private readonly ILiveKitTokenService _liveKitTokenService;
    private readonly IBaseRepository _baseRepo;
    private readonly IHubContext<ConsultationHub> _consultationHub;
    private readonly IHubContext<LiveReelHub> _liveReelHub;

    public ConsultationRoomController(
        IConsultationRepository consultationRepo,
        IDoctorRepository doctorRepo,
        ILiveKitTokenService liveKitTokenService,
        IBaseRepository baseRepo,
        IHubContext<ConsultationHub> consultationHub,
        IHubContext<LiveReelHub> liveReelHub)
    {
        _consultationRepo = consultationRepo;
        _doctorRepo = doctorRepo;
        _liveKitTokenService = liveKitTokenService;
        _baseRepo = baseRepo;
        _consultationHub = consultationHub;
        _liveReelHub = liveReelHub;
    }

    [HttpGet("/Consultation/Room/{id:long}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Room(long id)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long currentUserId);
        string currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";
        string currentUserName = User.Identity?.Name ?? "Member";

        using var conn = _baseRepo.CreateConnection();
        string sql = @"
            SELECT 
                c.ConsultationId,
                c.UserId AS PatientUserId,
                pu.FullName AS PatientName,
                pu.AvatarUrl AS PatientPhoto,
                pu.Email AS PatientEmail,
                pu.Phone AS PatientPhone,
                ISNULL(uw.Balance, 0.00) AS PatientWalletBalance,
                c.DoctorId,
                d.UserId AS DoctorUserId,
                du.FullName AS DoctorName,
                du.AvatarUrl AS DoctorPhoto,
                d.Specialty,
                d.Department,
                c.Status,
                c.StartedAt,
                c.RatePerMinute,
                c.DurationSeconds,
                c.TotalAmount,
                c.IsPaid,
                c.CreatedAt
            FROM dbo.Consultations c
            INNER JOIN dbo.Users pu ON c.UserId = pu.UserId
            LEFT JOIN dbo.UserWallets uw ON pu.UserId = uw.UserId
            INNER JOIN dbo.Doctors d ON c.DoctorId = d.DoctorId
            INNER JOIN dbo.Users du ON d.UserId = du.UserId
            WHERE c.ConsultationId = @ConsultationId;";

        var record = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { ConsultationId = id });
        if (record == null)
        {
            TempData["ErrorMessage"] = "Consultation session not found.";
            return RedirectToAction("Index", "Home");
        }

        long patientUserId = (long)record.PatientUserId;
        long doctorUserId = (long)record.DoctorUserId;
        bool isDoctor = (currentUserId == doctorUserId) || currentRole == "Doctor";
        bool isPatient = (currentUserId == patientUserId);
        bool isAdmin = currentRole == "Admin";

        if (!isDoctor && !isPatient && !isAdmin)
        {
            return Forbid();
        }

        string roomName = $"consultation_{id}";
        string participantIdentity = isDoctor ? $"doc_{currentUserId}" : $"user_{currentUserId}";
        string participantDisplayName = isDoctor 
            ? (record.DoctorName ?? $"Dr. {currentUserName}")
            : (record.PatientName ?? currentUserName);

        string token = _liveKitTokenService.GenerateAccessToken(
            roomName,
            participantIdentity,
            participantDisplayName,
            canPublish: true,
            canSubscribe: true);

        var vm = new ConsultationRoomViewModel
        {
            ConsultationId = id,
            PatientUserId = patientUserId,
            PatientName = (string)record.PatientName,
            PatientPhoto = (string?)record.PatientPhoto,
            PatientWalletBalance = (decimal)record.PatientWalletBalance,
            DoctorId = (long)record.DoctorId,
            DoctorUserId = doctorUserId,
            DoctorName = (string)record.DoctorName,
            DoctorPhoto = (string?)record.DoctorPhoto,
            Specialty = (string)record.Specialty,
            Department = (string)record.Department,
            Status = (string)record.Status,
            RatePerMinute = (decimal)record.RatePerMinute,
            IsPaid = (bool)record.IsPaid,
            StartedAt = record.StartedAt != null ? (DateTime)record.StartedAt : null,
            DurationSeconds = (int)(record.DurationSeconds ?? 0),
            TotalAmount = (decimal)(record.TotalAmount ?? 0.00m),

            IsDoctor = isDoctor,
            IsPatient = isPatient,
            CurrentRole = currentRole,
            CurrentUserId = currentUserId,
            CurrentUserName = currentUserName,

            LiveKitRoomName = roomName,
            LiveKitToken = token,
            LiveKitServerUrl = _liveKitTokenService.GetServerUrl()
        };

        return View("Index", vm);
    }
}

public class ConsultationRoomViewModel
{
    public long ConsultationId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhoto { get; set; }
    public decimal PatientWalletBalance { get; set; }
    public long DoctorId { get; set; }
    public long DoctorUserId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? DoctorPhoto { get; set; }
    public string Specialty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public decimal RatePerMinute { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? StartedAt { get; set; }
    public int DurationSeconds { get; set; }
    public decimal TotalAmount { get; set; }

    public bool IsDoctor { get; set; }
    public bool IsPatient { get; set; }
    public string CurrentRole { get; set; } = "User";
    public long CurrentUserId { get; set; }
    public string CurrentUserName { get; set; } = string.Empty;

    public string LiveKitRoomName { get; set; } = string.Empty;
    public string LiveKitToken { get; set; } = string.Empty;
    public string LiveKitServerUrl { get; set; } = string.Empty;
}
