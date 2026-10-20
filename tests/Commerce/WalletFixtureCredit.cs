using System.Text.Json;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;

namespace Kohami.M0.Commerce.Tests;

internal static class WalletFixtureCredit
{
    public static async Task VerifyMissingMemberRejectedAsync(string configPath)
    {
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var root = config.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean())
            throw new InvalidOperationException("DEDICATED_SQL_FIXTURE_REQUIRED");
        var settings = M0Settings.Load(configPath);
        var connection = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        if (!string.Equals(connection.DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(connection.InitialCatalog, "KohamiM0", StringComparison.Ordinal))
            throw new InvalidOperationException("WALLET_SYNTHETIC_CREDIT_SCOPE_INVALID");
        connection["Server Certificate"] = settings.SqlCertificatePath;

        await using var sql = new SqlConnection(connection.ConnectionString);
        await sql.OpenAsync();
        await using var transaction = await sql.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(
                "EXEC m0commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd",
                sql,
                (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@UserId", "missing_synthetic_wallet_member");
            command.Parameters.AddWithValue("@SourceId", "synthetic-test-credit:negative-member-check");
            command.Parameters.AddWithValue("@AmountTwd", 1L);
            await command.ExecuteNonQueryAsync();
            await transaction.RollbackAsync();
            throw new InvalidOperationException("WALLET_SYNTHETIC_MEMBER_GUARD_FAILED");
        }
        catch (SqlException error) when (error.Number == 51000 && error.Message.Contains("WALLET_SYNTHETIC_MEMBER_REQUIRED", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync();
            Console.WriteLine("PASS kohami-sql:14349 synthetic credit rejects a missing member without writes");
        }
    }

    public static async Task RunAsync(string configPath, string userId, long amountTwd, string sourceId)
    {
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var root = config.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean())
            throw new InvalidOperationException("DEDICATED_SQL_FIXTURE_REQUIRED");

        var settings = M0Settings.Load(configPath);
        var connection = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        if (!string.Equals(connection.DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(connection.InitialCatalog, "KohamiM0", StringComparison.Ordinal)
            || amountTwd is <= 0 or > 50000
            || string.IsNullOrWhiteSpace(userId)
            || !sourceId.StartsWith("synthetic-test-credit:", StringComparison.Ordinal)
            || sourceId.Length > 100)
            throw new InvalidOperationException("WALLET_SYNTHETIC_CREDIT_SCOPE_INVALID");
        connection["Server Certificate"] = settings.SqlCertificatePath;

        await using var sql = new SqlConnection(connection.ConnectionString);
        await sql.OpenAsync();
        await using var transaction = await sql.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(
                "EXEC m0commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd",
                sql,
                (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@SourceId", sourceId);
            command.Parameters.AddWithValue("@AmountTwd", amountTwd);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        Console.WriteLine("PASS synthetic wallet credit committed to marker-guarded localhost:14349 fixture");
    }
}
