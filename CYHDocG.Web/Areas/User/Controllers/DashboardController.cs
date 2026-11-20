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
        return View();
    }
}
