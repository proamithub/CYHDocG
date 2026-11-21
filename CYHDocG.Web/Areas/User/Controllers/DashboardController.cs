using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = "User")]
public class DashboardController : Controller
{
    private readonly IWalletRepository _walletRepo;
    private readonly IPrescriptionRepository _prescriptionRepo;

    public DashboardController(IWalletRepository walletRepo, IPrescriptionRepository prescriptionRepo)
    {
        _walletRepo = walletRepo;
        _prescriptionRepo = prescriptionRepo;
    }

    public async Task<IActionResult> Index()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var wallet = await _walletRepo.GetBalanceAsync(userId);
        ViewBag.WalletBalance = wallet?.Balance ?? 0.00m;

        try
        {
            var prescriptions = (await _prescriptionRepo.GetUserPrescriptionsAsync(userId))?.ToList() ?? new List<CYHDocG.Repo.DTOs.PrescriptionHeaderDto>();
            ViewBag.ActivePrescriptionsCount = prescriptions.Count;

            var activeMedicines = prescriptions.SelectMany(p => p.Medicines ?? new List<CYHDocG.Repo.DTOs.PrescriptionMedicineItemDto>()).ToList();
            var now = DateTime.UtcNow;

            ViewBag.DosesDueCount = activeMedicines.Count(m => m.CourseEndDate >= now);
            ViewBag.RefillAlertsCount = activeMedicines.Count(m => m.RefillAlertDate <= now && m.CourseEndDate >= now);
        }
        catch
        {
            ViewBag.ActivePrescriptionsCount = 0;
            ViewBag.DosesDueCount = 0;
            ViewBag.RefillAlertsCount = 0;
        }

        return View();
    }
}
