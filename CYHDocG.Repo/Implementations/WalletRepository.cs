using Dapper;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class WalletRepository : BaseRepository, IWalletRepository
{
    public WalletRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<WalletBalanceDto?> GetBalanceAsync(long userId)
    {
        return await QueryFirstOrDefaultSPAsync<WalletBalanceDto>("dbo.usp_Wallet_GetBalance", new { UserId = userId });
    }

    public async Task<ApiResponse<decimal>> TopUpAsync(WalletTopUpDto request)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Wallet_TopUp", new
        {
            UserId = request.UserId,
            Amount = request.Amount,
            ReferenceId = request.ReferenceId,
            Description = request.Description
        });

        if (result != null && (int)result.Success == 1)
        {
            return ApiResponse<decimal>.Ok((decimal)result.NewBalance, (string)result.Message);
        }
        return ApiResponse<decimal>.Fail(result != null ? (string)result.Message : "Wallet top-up failed", 0.00m);
    }

    public async Task<IEnumerable<WalletTransactionDto>> GetTransactionHistoryAsync(long userId, int pageNumber = 1, int pageSize = 20)
    {
        return await QuerySPAsync<WalletTransactionDto>("dbo.usp_Wallet_GetTransactionHistory", new
        {
            UserId = userId,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
    }
}
