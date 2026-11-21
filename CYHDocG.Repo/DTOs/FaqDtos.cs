namespace CYHDocG.Repo.DTOs;

public class FaqDto
{
    public long FaqId { get; set; }
    public string Category { get; set; } = "General";
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
