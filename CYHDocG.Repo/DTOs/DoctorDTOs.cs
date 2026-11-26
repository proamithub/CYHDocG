namespace CYHDocG.Repo.DTOs;

public class DoctorReelDto
{
    public long DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? DoctorPhoto { get; set; }
    public string Specialty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public decimal ConsultationFeePerMinute { get; set; }
    public bool IsFreeConsultation { get; set; }
    public bool IsLive { get; set; }
    public string? LiveStreamUrl { get; set; }
    public DateTime? ScheduledLiveAt { get; set; }
    public int FollowerCount { get; set; }
    public decimal Rating { get; set; }
    public long? StreamId { get; set; }
    public int CurrentViewers { get; set; }
    public int TotalLikes { get; set; }
    public bool IsFollowing { get; set; }
    public bool HasLiked { get; set; }

    // Encrypted token for tamper-proof frontend sharing & action links
    public string EncryptedDoctorId => Security.UrlProtector.EncryptId(DoctorId);
}

public class DoctorFollowResultDto
{
    public bool IsFollowing { get; set; }
    public int FollowerCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DoctorSearchDto
{
    public long DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public decimal ConsultationFeePerMinute { get; set; }
    public bool IsFreeConsultation { get; set; }
    public bool IsLive { get; set; }
    public string? AvatarUrl { get; set; }
    public string EncryptedDoctorId => Security.UrlProtector.EncryptId(DoctorId);
}

public class LiveCommentDto
{
    public long CommentId { get; set; }
    public long UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class AddCommentDto
{
    public long StreamId { get; set; }
    public long UserId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DoctorAdminListDto
{
    public long DoctorId { get; set; }
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public decimal ConsultationFeePerMinute { get; set; }
    public bool IsFreeConsultation { get; set; }
    public bool IsLive { get; set; }
    public bool IsApproved { get; set; }
    public decimal Rating { get; set; }
    public int FollowerCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminDashboardMetricsDto
{
    public int TotalDoctors { get; set; }
    public int LiveDoctors { get; set; }
    public int ConsultationsToday { get; set; }
    public decimal PlatformRevenue { get; set; }
    public int PrescriptionsIssued { get; set; }
    public int RemindersDue { get; set; }
}

public class DoctorStudioMetricsDto
{
    public long DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public int FollowerCount { get; set; }
    public int ConsultationsToday { get; set; }
    public decimal ConsultationFeePerMinute { get; set; }
    public bool IsFreeConsultation { get; set; }
    public bool IsLive { get; set; }
    public decimal WalletBalance { get; set; }
    public bool IsApproved { get; set; }
}

public class PinnedBannerDto
{
    public long DoctorId { get; set; }
    public string BannerText { get; set; } = string.Empty;
    public string BannerType { get; set; } = "tip"; // tip, medicine, diet, warning
    public string? MedicineName { get; set; }
    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
}

public class LiveStreamSessionDto
{
    public long StreamId { get; set; }
    public long DoctorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
    public int CurrentViewers { get; set; }
    public int TotalLikes { get; set; }
    public int TotalComments { get; set; }
    public DateTime CreatedAt { get; set; }

    public int DurationMinutes
    {
        get
        {
            var start = StartedAt ?? CreatedAt;
            var end = EndedAt ?? (IsActive ? DateTime.UtcNow : start);
            var span = end - start;
            return span.TotalMinutes > 0 ? (int)span.TotalMinutes : 0;
        }
    }
}

public class DoctorEarningsSummaryDto
{
    public long DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public decimal AvailableBalance { get; set; }
    public decimal LifetimeWithdrawn { get; set; }
    public decimal TotalNetEarned { get; set; }
    public int MonthSessionsCount { get; set; }
    public double MonthAvgDurationMinutes { get; set; }
    public decimal MonthGrossEarnings { get; set; }
    public decimal MonthNetEarnings { get; set; }
    public List<DoctorConsultationSettlementDto> RecentConsultations { get; set; } = new();
    public List<DoctorPayoutItemDto> PayoutHistory { get; set; } = new();
}

public class DoctorConsultationSettlementDto
{
    public long ConsultationId { get; set; }
    public long PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientAvatar { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DurationSeconds { get; set; }
    public decimal RatePerMinute { get; set; }
    public decimal GrossFee { get; set; }
    public decimal DoctorNetCut { get; set; }
    public bool IsPaid { get; set; }
    public string Status { get; set; } = string.Empty;

    public int DurationMinutes => Math.Max(1, (int)Math.Ceiling(DurationSeconds / 60.0));
}

public class DoctorPayoutRequestDto
{
    public decimal Amount { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? UpiId { get; set; }
}

public class DoctorPayoutItemDto
{
    public long PayoutRequestId { get; set; }
    public long DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? UpiId { get; set; }
    public string? AdminRemarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class PlatformLedgerItemDto
{
    public long LedgerId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string? FromUserName { get; set; }
    public string? ToDoctorName { get; set; }
    public decimal Amount { get; set; }
    public decimal PlatformFee { get; set; }
    public string? ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminLedgerPageDto
{
    public decimal GrossConsultationVolume { get; set; }
    public decimal PlatformNetRevenue { get; set; }
    public decimal DoctorEarningsCredited { get; set; }
    public decimal DoctorAvailablePayoutBalance { get; set; }
    public decimal TotalDoctorWithdrawn { get; set; }
    public int PendingPayoutsCount { get; set; }
    public decimal PendingPayoutsAmount { get; set; }
    public List<DoctorSearchDto> Doctors { get; set; } = new();
    public List<DoctorPayoutItemDto> PayoutRequests { get; set; } = new();
    public List<PlatformLedgerItemDto> RecentLedgerEntries { get; set; } = new();
}
