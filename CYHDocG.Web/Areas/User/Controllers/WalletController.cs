using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = "User")]
public class WalletController : Controller
{
    private readonly IWalletRepository _walletRepo;

    public WalletController(IWalletRepository walletRepo)
    {
        _walletRepo = walletRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var wallet = await _walletRepo.GetBalanceAsync(userId);
        var transactions = await _walletRepo.GetTransactionHistoryAsync(userId, pageNumber: 1, pageSize: 20);

        ViewBag.WalletBalance = wallet?.Balance ?? 0.00m;
        return View(transactions.ToList());
    }
}
