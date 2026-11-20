using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class BaseRepository : IBaseRepository
{
    private readonly string _connectionString;

    public BaseRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=208.91.198.196;Database=CYHDOCGDB;User Id=CYHDOCGUDB;Password=CYHDOCGPASSDB;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False";
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public async Task<IEnumerable<T>> QuerySPAsync<T>(string spName, object? parameters = null)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<T>(spName, parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<T?> QueryFirstOrDefaultSPAsync<T>(string spName, object? parameters = null)
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<T>(spName, parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> ExecuteSPAsync(string spName, object? parameters = null)
    {
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(spName, parameters, commandType: CommandType.StoredProcedure);
    }
}
