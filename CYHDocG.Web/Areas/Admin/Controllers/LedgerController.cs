using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class LedgerController : Controller
{
    private readonly IDoctorRepository _doctorRepo;
    private readonly ISettingsRepository _settingsRepo;

    public LedgerController(IDoctorRepository doctorRepo, ISettingsRepository settingsRepo)
    {
        _doctorRepo = doctorRepo;
        _settingsRepo = settingsRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var doctors = await _doctorRepo.SearchDoctorsAsync("");
        return View(doctors.ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePricing(long doctorId, bool isFree, decimal feePerMinute)
    {
        var result = await _doctorRepo.UpdateDoctorPricingAsync(doctorId, isFree, feePerMinute);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
