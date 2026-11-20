using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        var balance = await _walletRepo.GetBalanceAsync(userId);
        return Ok(balance);
    }

    [HttpPost("topup")]
    public async Task<IActionResult> TopUp([FromBody] WalletTopUpDto request)
    {
        var result = await _walletRepo.TopUpAsync(request);
        return Ok(result);
    }

    [HttpGet("{userId}/transactions")]
    public async Task<IActionResult> GetTransactions(long userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var txs = await _walletRepo.GetTransactionHistoryAsync(userId, page, pageSize);
        return Ok(txs);
    }
}
