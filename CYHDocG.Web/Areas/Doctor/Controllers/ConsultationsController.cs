using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class ConsultationsController : Controller
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IDoctorRepository _doctorRepo;
    private readonly IBaseRepository _baseRepo;

    public ConsultationsController(IConsultationRepository consultationRepo, IDoctorRepository doctorRepo, IBaseRepository baseRepo)
    {
        _consultationRepo = consultationRepo;
        _doctorRepo = doctorRepo;
        _baseRepo = baseRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);
        
        if (metrics == null || !metrics.IsApproved)
        {
            TempData["ErrorMessage"] = "Your doctor account is pending Admin verification. Patient Queue is currently locked.";
            return RedirectToAction("Index", "Dashboard");
        }

        long doctorId = metrics?.DoctorId ?? userId;

        using var conn = _baseRepo.CreateConnection();
        string sql = @"
            SELECT 
                c.ConsultationId,
                c.UserId,
                u.FullName AS PatientName,
                u.Email AS PatientEmail,
                ISNULL(u.Phone, '') AS PatientPhone,
                c.Status,
                c.RatePerMinute,
                c.DurationSeconds,
                c.TotalAmount,
                c.CreatedAt,
                ISNULL(uw.Balance, 0.00) AS PatientWalletBalance
            FROM dbo.Consultations c
            INNER JOIN dbo.Users u ON c.UserId = u.UserId
            LEFT JOIN dbo.UserWallets uw ON u.UserId = uw.UserId
            WHERE c.DoctorId = @DoctorId
            ORDER BY 
                CASE WHEN c.Status = 'Pending' THEN 1 WHEN c.Status = 'Active' THEN 2 ELSE 3 END,
                c.CreatedAt DESC;";

        var list = (await conn.QueryAsync<DoctorConsultationQueueItemDto>(sql, new { DoctorId = doctorId })).ToList();
        ViewBag.DoctorId = doctorId;
        return View(list);
    }
}

public class DoctorConsultationQueueItemDto
{
    public long ConsultationId { get; set; }
    public long UserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public decimal RatePerMinute { get; set; }
    public int DurationSeconds { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal PatientWalletBalance { get; set; }
}
