using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletsController : ControllerBase
{
    private readonly IWalletRepository _walletRepo;

    public WalletsController(IWalletRepository walletRepo)
    {
        _walletRepo = walletRepo;
    }

    [HttpGet("{userId}/balance")]
    public async Task<IActionResult> GetBalance(long userId)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        if (callerUserId != userId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var balance = await _walletRepo.GetBalanceAsync(userId);
        return Ok(balance);
    }

    [HttpPost("topup")]
    public async Task<IActionResult> TopUp([FromBody] WalletTopUpDto request)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        if (!User.IsInRole("Admin"))
        {
            request.UserId = callerUserId;
        }

        var result = await _walletRepo.TopUpAsync(request);
        return Ok(result);
    }

    [HttpGet("{userId}/transactions")]
    public async Task<IActionResult> GetTransactions(long userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long callerUserId) || callerUserId <= 0)
        {
            return Unauthorized();
        }

        if (callerUserId != userId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var txs = await _walletRepo.GetTransactionHistoryAsync(userId, page, pageSize);
        return Ok(txs);
    }
}
