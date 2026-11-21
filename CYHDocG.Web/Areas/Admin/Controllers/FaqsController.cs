using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class FaqsController : Controller
{
    private readonly IFaqRepository _faqRepo;

    public FaqsController(IFaqRepository faqRepo)
    {
        _faqRepo = faqRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? q, [FromQuery] string? cat)
    {
        var faqs = await _faqRepo.GetAllAdminFaqsAsync(q, cat);
        ViewBag.SearchTerm = q ?? "";
        ViewBag.SelectedCategory = cat ?? "";
        return View(faqs.ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(FaqDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Question) || string.IsNullOrWhiteSpace(model.Answer))
        {
            TempData["ErrorMessage"] = "Question and Answer are required.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _faqRepo.SaveFaqAsync(model);
            TempData["SuccessMessage"] = model.FaqId > 0 ? "FAQ updated successfully!" : "New FAQ created successfully!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error saving FAQ: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(long id)
    {
        await _faqRepo.ToggleFaqStatusAsync(id);
        TempData["SuccessMessage"] = "FAQ status updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _faqRepo.DeleteFaqAsync(id);
        if (deleted)
        {
            TempData["SuccessMessage"] = "FAQ deleted successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not delete FAQ.";
        }
        return RedirectToAction(nameof(Index));
    }
}
