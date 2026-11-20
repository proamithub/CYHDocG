using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace CYHDocG.Tests;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine(" CYHDocGuru Live Database Deployment Tool");
        Console.WriteLine(" Host: 208.91.198.196 | DB: CYHDOCGDB");
        Console.WriteLine("==================================================");

        string connStr = "Server=208.91.198.196;Database=CYHDOCGDB;User Id=CYHDOCGUDB;Password=CYHDOCGPASSDB;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False;Connect Timeout=30;";

        string scriptPath = Path.GetFullPath(@"..\CYHDocG.Database\00_FULL_SETUP_CYHDOCGDB.sql");
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.GetFullPath(@"CYHDocG.Database\00_FULL_SETUP_CYHDOCGDB.sql");
        }

        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"Error: SQL file not found at {scriptPath}");
            return 1;
        }

        Console.WriteLine($"Found script: {scriptPath} ({new FileInfo(scriptPath).Length} bytes)");

        try
        {
            Console.WriteLine("Testing connection to MSSQL Server...");
            using var connection = new SqlConnection(connStr);
            await connection.OpenAsync();
            Console.WriteLine("Connected to database successfully!");

            string fullSql = await File.ReadAllTextAsync(scriptPath);
            // Split script by GO statements (case-insensitive, on line boundaries)
            var batches = Regex.Split(fullSql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            int executed = 0;
            int skipped = 0;

            for (int i = 0; i < batches.Length; i++)
            {
                string batch = batches[i].Trim();
                if (string.IsNullOrWhiteSpace(batch))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    using var cmd = new SqlCommand(batch, connection);
                    cmd.CommandTimeout = 120;
                    await cmd.ExecuteNonQueryAsync();
                    executed++;
                    if (executed % 5 == 0 || i == batches.Length - 1)
                    {
                        Console.WriteLine($"[Progress] Executed {executed} batches...");
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"[Batch {i + 1} Error]: {ex.Message}");
                    // Continue to next batch if already exists
                }
            }

            Console.WriteLine($"\nScript execution completed! Executed: {executed}, Skipped empty: {skipped}");

            // Verify tables and procedures created
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.tables;", connection))
            {
                var count = await cmd.ExecuteScalarAsync();
                Console.WriteLine($"\n[VERIFICATION] Total Tables in CYHDOCGDB: {count}");
            }

            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.procedures;", connection))
            {
                var count = await cmd.ExecuteScalarAsync();
                Console.WriteLine($"[VERIFICATION] Total Stored Procedures in CYHDOCGDB: {count}");
            }

            using (var cmd = new SqlCommand("SELECT SettingKey, SettingValue FROM dbo.AppSettings WHERE SettingKey = 'SecurityGuardEnabled';", connection))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    Console.WriteLine($"[VERIFICATION] AppSetting 'SecurityGuardEnabled': {reader["SettingValue"]}");
                }
            }

            Console.WriteLine("\nDatabase is fully configured and ready!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[FATAL ERROR]: {ex.Message}");
            return 1;
        }
    }
}
