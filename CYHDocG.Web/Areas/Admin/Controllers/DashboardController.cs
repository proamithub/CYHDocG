using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly IDoctorRepository _doctorRepo;

    public DashboardController(ISettingsRepository settingsRepo, IDoctorRepository doctorRepo)
    {
        _settingsRepo = settingsRepo;
        _doctorRepo = doctorRepo;
    }

    public async Task<IActionResult> Index()
    {
        var antiInspect = await _settingsRepo.GetSettingByKeyAsync("AntiInspectEnabled");
        ViewBag.AntiInspectEnabled = antiInspect == null || (antiInspect.IsActive && antiInspect.SettingValue == "1");

        var metrics = await _doctorRepo.GetAdminDashboardMetricsAsync();
        return View(metrics);
    }
}
