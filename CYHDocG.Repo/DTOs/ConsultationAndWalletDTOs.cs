namespace CYHDocG.Repo.DTOs;

public class ConsultationRequestDto
{
    public long UserId { get; set; }
    public long DoctorId { get; set; }
}

public class VideoTokenRequestDto
{
    public long ConsultationId { get; set; }
    public string? RoomName { get; set; }
}

public class ConsultationActiveDto
{
    public long ConsultationId { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public long DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public decimal RatePerMinute { get; set; }
    public bool IsPaid { get; set; }
    public string Status { get; set; } = "Active";
}

public class ConsultationMessageDto
{
    public long MessageId { get; set; }
    public long ConsultationId { get; set; }
    public long SenderId { get; set; }
    public string SenderRole { get; set; } = "User"; // 'User', 'Doctor', 'AI'
    public string MessageText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ConsultationEndDto
{
    public long ConsultationId { get; set; }
    public int DurationSeconds { get; set; }
    public decimal BilledAmount { get; set; }
}

public class WalletBalanceDto
{
    public long WalletId { get; set; }
    public long UserId { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime UpdatedAt { get; set; }
}

public class WalletTopUpDto
{
    public long UserId { get; set; }
    public decimal Amount { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public string Description { get; set; } = "Wallet Top-Up";
}

public class WalletTransactionDto
{
    public long TransactionId { get; set; }
    public string Type { get; set; } = string.Empty; // 'Credit', 'Debit'
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
