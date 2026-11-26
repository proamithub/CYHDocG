using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class LedgerController : Controller
{
    private readonly IDoctorRepository _doctorRepo;

    public LedgerController(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await _doctorRepo.GetAdminLedgerDataAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePricing(long doctorId, bool isFree, decimal feePerMinute)
    {
        var result = await _doctorRepo.UpdateDoctorPricingAsync(doctorId, isFree, feePerMinute);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayout(long payoutRequestId, string payoutAction, string? adminRemarks)
    {
        var result = await _doctorRepo.ProcessDoctorPayoutAsync(payoutRequestId, payoutAction, adminRemarks);
        if (result.Success)
        {
            TempData["SuccessMessage"] = result.Message;
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
