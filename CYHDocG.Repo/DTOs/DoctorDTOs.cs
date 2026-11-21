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
}

public class PinnedBannerDto
{
    public long DoctorId { get; set; }
    public string BannerText { get; set; } = string.Empty;
    public string BannerType { get; set; } = "tip"; // tip, medicine, diet, warning
    public string? MedicineName { get; set; }
    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
}
