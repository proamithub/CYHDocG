using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletRepository _walletRepo;

    public WalletController(IWalletRepository walletRepo)
    {
        _walletRepo = walletRepo;
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var balance = await _walletRepo.GetBalanceAsync(userId);
        return Ok(balance);
    }

    [HttpPost("topup")]
    public async Task<IActionResult> TopUp([FromBody] WalletTopUpDto request)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        request.UserId = userId;
        var result = await _walletRepo.TopUpAsync(request);
        return Ok(result);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var txs = await _walletRepo.GetTransactionHistoryAsync(userId, pageNumber, pageSize);
        return Ok(txs);
    }
}
