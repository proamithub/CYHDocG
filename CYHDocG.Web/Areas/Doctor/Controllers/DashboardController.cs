using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class DashboardController : Controller
{
    private readonly IDoctorRepository _doctorRepo;

    public DashboardController(IDoctorRepository doctorRepo)
    {
        _doctorRepo = doctorRepo;
    }

    public async Task<IActionResult> Index()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var metrics = await _doctorRepo.GetDoctorStudioMetricsAsync(userId);
        return View(metrics);
    }
}
