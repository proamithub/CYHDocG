using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IFaqRepository
{
    Task<IEnumerable<FaqDto>> GetActiveFaqsAsync(string? category = null);
    Task<IEnumerable<FaqDto>> GetAllAdminFaqsAsync(string? searchTerm = null, string? category = null);
    Task<FaqDto?> GetFaqByIdAsync(long faqId);
    Task<long> SaveFaqAsync(FaqDto faq);
    Task<bool> ToggleFaqStatusAsync(long faqId);
    Task<bool> DeleteFaqAsync(long faqId);
}
