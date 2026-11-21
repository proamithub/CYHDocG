using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DoctorsController : Controller
{
    private readonly IDoctorRepository _doctorRepo;

    public DoctorsController(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var doctors = await _doctorRepo.GetAllDoctorsForAdminAsync();
        return View(doctors.ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleApproval(long doctorId, bool isApproved)
    {
        var result = await _doctorRepo.ToggleDoctorApprovalAsync(doctorId, isApproved);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
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
