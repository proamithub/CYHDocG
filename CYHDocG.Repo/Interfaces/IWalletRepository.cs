using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IWalletRepository
{
    Task<WalletBalanceDto?> GetBalanceAsync(long userId);
    Task<ApiResponse<decimal>> TopUpAsync(WalletTopUpDto request);
    Task<IEnumerable<WalletTransactionDto>> GetTransactionHistoryAsync(long userId, int pageNumber = 1, int pageSize = 20);
}
