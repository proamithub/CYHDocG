using Dapper;
using System.Data;

namespace CYHDocG.Repo.Interfaces;

public interface IBaseRepository
{
    IDbConnection CreateConnection();
    Task<IEnumerable<T>> QuerySPAsync<T>(string spName, object? parameters = null);
    Task<T?> QueryFirstOrDefaultSPAsync<T>(string spName, object? parameters = null);
    Task<int> ExecuteSPAsync(string spName, object? parameters = null);
}
