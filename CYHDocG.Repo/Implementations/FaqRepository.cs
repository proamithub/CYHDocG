using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class FaqRepository : IFaqRepository
{
    private readonly IBaseRepository _baseRepo;

    public FaqRepository(IBaseRepository baseRepo)
    {
        _baseRepo = baseRepo;
    }

    public async Task<IEnumerable<FaqDto>> GetActiveFaqsAsync(string? category = null)
    {
        var parameters = new { Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim() };
        return await _baseRepo.QuerySPAsync<FaqDto>("dbo.usp_Faq_GetActive", parameters);
    }

    public async Task<IEnumerable<FaqDto>> GetAllAdminFaqsAsync(string? searchTerm = null, string? category = null)
    {
        var parameters = new
        {
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim()
        };
        return await _baseRepo.QuerySPAsync<FaqDto>("dbo.usp_Faq_GetAllAdmin", parameters);
    }

    public async Task<FaqDto?> GetFaqByIdAsync(long faqId)
    {
        return await _baseRepo.QueryFirstOrDefaultSPAsync<FaqDto>("dbo.usp_Faq_GetById", new { FaqId = faqId });
    }

    public async Task<long> SaveFaqAsync(FaqDto faq)
    {
        var parameters = new
        {
            FaqId = faq.FaqId,
            Category = string.IsNullOrWhiteSpace(faq.Category) ? "General" : faq.Category.Trim(),
            Question = faq.Question.Trim(),
            Answer = faq.Answer.Trim(),
            DisplayOrder = faq.DisplayOrder,
            IsActive = faq.IsActive
        };

        var result = await _baseRepo.QueryFirstOrDefaultSPAsync<long?>("dbo.usp_Faq_Save", parameters);
        return result ?? faq.FaqId;
    }

    public async Task<bool> ToggleFaqStatusAsync(long faqId)
    {
        var result = await _baseRepo.QueryFirstOrDefaultSPAsync<bool?>("dbo.usp_Faq_ToggleStatus", new { FaqId = faqId });
        return result ?? false;
    }

    public async Task<bool> DeleteFaqAsync(long faqId)
    {
        var rows = await _baseRepo.ExecuteSPAsync("dbo.usp_Faq_Delete", new { FaqId = faqId });
        return rows > 0;
    }
}
