using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly ISettingsRepository _settingsRepo;

    public DashboardController(ISettingsRepository settingsRepo)
    {
        _settingsRepo = settingsRepo;
    }

    public async Task<IActionResult> Index()
    {
        var antiInspect = await _settingsRepo.GetSettingByKeyAsync("AntiInspectEnabled");
        ViewBag.AntiInspectEnabled = antiInspect == null || (antiInspect.IsActive && antiInspect.SettingValue == "1");
        return View();
    }
}
