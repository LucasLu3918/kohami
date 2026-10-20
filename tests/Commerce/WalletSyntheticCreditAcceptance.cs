using System.Text.Json;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;

namespace Kohami.M0.Commerce.Tests;

public static class WalletSyntheticCreditAcceptance
{
    private sealed record WalletState(bool Exists, long Balance, int EntryCount);
    private sealed record CreditAttempt(bool Committed, bool ReplayRejected);
    private sealed record SyntheticCreditRow(string EntryId, string UserId, long AmountTwd, string Currency);

    public static async Task RunAsync(string configPath)
    {
        var (settings, connectionString) = ReadDedicatedFixture(configPath);
        var suffix = Guid.NewGuid().ToString("N");
        var memberA = "wallet_synth_a_" + suffix;
        var memberB = "wallet_synth_b_" + suffix;
        var memberRace = "wallet_synth_r_" + suffix;
        var members = new Dictionary<string, string>
        {
            [memberA] = memberA + "@example.test",
            [memberB] = memberB + "@example.test",
            [memberRace] = memberRace + "@example.test"
        };
        var source = "synthetic-test-credit:issue02:" + suffix;
        var raceSource = "synthetic-test-credit:issue02-race:" + suffix;
        var initialCredit = 7000L;
        var raceCredit = 3200L;

        await VerifyDedicatedMarkerAsync(connectionString, settings);
        var maintenanceBefore = await ReadMaintenanceAsync(connectionString);
        if (maintenanceBefore)
            throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_MAINTENANCE_ENABLED");

        try
        {
            await InsertSyntheticMembersAsync(connectionString, members);

            await CreditAsync(connectionString, memberA, source, initialCredit);
            var responseLostLookup = await ReadCreditBySourceAsync(connectionString, source);
            Check(responseLostLookup is { UserId: var recoveredUser, AmountTwd: var recoveredAmount, Currency: "TWD" }
                && recoveredUser == memberA && recoveredAmount == initialCredit
                && await CountSourceAsync(connectionString, source) == 1,
                "source lookup recovers the original credit after a lost response without a second entry");

            var invalidSource = "issue02-invalid-source:" + suffix;
            var beforeInvalidSource = await ReadWalletStateAsync(connectionString, memberB);
            var invalidSourceRejected = await RejectInvalidSourceAsync(
                connectionString, memberB, invalidSource, initialCredit);
            var afterInvalidSource = await ReadWalletStateAsync(connectionString, memberB);
            Check(invalidSourceRejected
                && beforeInvalidSource == afterInvalidSource
                && !afterInvalidSource.Exists
                && await CountSourceAsync(connectionString, invalidSource) == 0,
                "non-prefix source is rejected before creating wallet state");

            var beforeExactReplay = await ReadWalletStateAsync(connectionString, memberA);
            var exactReplay = await AttemptCreditAsync(connectionString, memberA, source, initialCredit);
            var afterExactReplay = await ReadWalletStateAsync(connectionString, memberA);
            Check(exactReplay.ReplayRejected && !exactReplay.Committed
                && beforeExactReplay == afterExactReplay
                && await CountSourceAsync(connectionString, source) == 1,
                "exact source replay is rejected without a second synthetic credit");

            var beforeMemberMismatch = await ReadWalletStateAsync(connectionString, memberA);
            var memberMismatch = await AttemptCreditAsync(connectionString, memberB, source, initialCredit);
            var afterMemberMismatch = await ReadWalletStateAsync(connectionString, memberA);
            var mismatchedMemberState = await ReadWalletStateAsync(connectionString, memberB);
            Check(memberMismatch.ReplayRejected && !memberMismatch.Committed
                && beforeMemberMismatch == afterMemberMismatch
                && !mismatchedMemberState.Exists
                && await CountSourceAsync(connectionString, source) == 1,
                "same source with a different member is rejected without changing either wallet");

            var beforeAmountMismatch = await ReadWalletStateAsync(connectionString, memberA);
            var amountMismatch = await AttemptCreditAsync(connectionString, memberA, source, initialCredit + 1);
            var afterAmountMismatch = await ReadWalletStateAsync(connectionString, memberA);
            Check(amountMismatch.ReplayRejected && !amountMismatch.Committed
                && beforeAmountMismatch == afterAmountMismatch
                && await CountSourceAsync(connectionString, source) == 1,
                "same source with a different amount is rejected without changing the ledger or balance");

            var beforeCurrencyMismatch = await ReadWalletStateAsync(connectionString, memberA);
            var currencyMismatchRejected = await RejectCurrencyMismatchAsync(
                connectionString, memberA, source, initialCredit, "walletusd_" + suffix);
            var afterCurrencyMismatch = await ReadWalletStateAsync(connectionString, memberA);
            Check(currencyMismatchRejected
                && beforeCurrencyMismatch == afterCurrencyMismatch
                && await CountSourceAsync(connectionString, source) == 1
                && await CountNonTwdRowsAsync(connectionString, memberA) == 0,
                "same source with a non-TWD currency cannot change the original synthetic credit");

            var currencyConstraintSource = source + ":currency";
            var nonTwdConstraintRejected = await RejectNonTwdCurrencyAsync(
                connectionString, memberA, currencyConstraintSource, initialCredit, "walletusdcheck_" + suffix);
            Check(nonTwdConstraintRejected
                && await CountSourceAsync(connectionString, currencyConstraintSource) == 0
                && await CountNonTwdRowsAsync(connectionString, memberA) == 0,
                "wallet ledger rejects a non-TWD currency independently of source replay");

            using (var ready = new CountdownEvent(2))
            {
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                async Task<CreditAttempt> RaceCreditAsync()
                {
                    ready.Signal();
                    await start.Task;
                    return await AttemptCreditAsync(connectionString, memberRace, raceSource, raceCredit);
                }

                var first = Task.Run(RaceCreditAsync);
                var second = Task.Run(RaceCreditAsync);
                if (!ready.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("WALLET_SYNTHETIC_SOURCE_RACE_NOT_READY");
                start.SetResult();
                var outcomes = await Task.WhenAll(first, second);
                var committed = outcomes.Count(outcome => outcome.Committed);
                var replayRejected = outcomes.Count(outcome => outcome.ReplayRejected);
                var raceWallet = await ReadWalletStateAsync(connectionString, memberRace);
                Check(committed == 1 && replayRejected == 1
                    && raceWallet.Exists && raceWallet.Balance == raceCredit
                    && raceWallet.EntryCount == 1
                    && await CountSourceAsync(connectionString, raceSource) == 1,
                    "concurrent identical source credits commit at most one ledger entry");
            }

            await DebitAsync(connectionString, memberA, "walletdebit_" + suffix,
                "checkout:" + suffix, 2100L);
            var expectedProjection = await ReadLedgerProjectionAsync(connectionString, memberA);
            Check(expectedProjection == initialCredit - 2100L,
                "synthetic credit and checkout debit produce the expected ledger projection");

            await SetProjectionForTestAsync(connectionString, memberA, 1L);
            await RebuildProjectionInFixtureAsync(connectionString, memberA);
            var rebuilt = await ReadWalletStateAsync(connectionString, memberA);
            var rebuiltLedgerProjection = await ReadLedgerProjectionAsync(connectionString, memberA);
            Check(rebuilt.Balance == expectedProjection && rebuilt.Balance == rebuiltLedgerProjection,
                "dedicated fixture projection rebuild matches SyntheticCredit minus CheckoutDebit");

            await RebuildProjectionInFixtureAsync(connectionString, memberA);
            var rebuiltAgain = await ReadWalletStateAsync(connectionString, memberA);
            Check(rebuiltAgain.Balance == expectedProjection
                && rebuiltAgain.EntryCount == rebuilt.EntryCount,
                "repeating projection rebuild does not change the ledger-derived balance");
        }
        finally
        {
            await CleanupAsync(connectionString, members);
            if (await ReadMaintenanceAsync(connectionString) != maintenanceBefore)
                throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_MAINTENANCE_CHANGED");
        }

        Console.WriteLine("PASS Issue02 synthetic wallet credit SQL acceptance on marker-verified localhost:14349/KohamiM0/demo_m0");
    }

    private static (M0Settings Settings, string ConnectionString) ReadDedicatedFixture(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        EnsurePrivateFile(fullPath);
        using var json = JsonDocument.Parse(File.ReadAllText(fullPath));
        var root = json.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean()
            || !root.TryGetProperty("datasetId", out var dataset) || dataset.GetString() != "demo_m0")
            throw new InvalidOperationException("DEDICATED_SQL_FIXTURE_REQUIRED");

        var settings = M0Settings.Load(fullPath);
        var connection = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName,
            ConnectTimeout = 15
        };
        if (!string.Equals(connection.DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(connection.InitialCatalog, "KohamiM0", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(settings.SqlCertificatePath)
            || string.IsNullOrWhiteSpace(settings.SqlServerName))
            throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_SCOPE_INVALID");
        connection["Server Certificate"] = settings.SqlCertificatePath;
        return (settings, connection.ConnectionString);
    }

    private static void EnsurePrivateFile(string path)
    {
        var file = new FileInfo(path);
        var directory = file.Directory ?? throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_PRIVATE_PROFILE_INVALID");
        if (!file.Exists || file.LinkTarget is not null || directory.LinkTarget is not null)
            throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_PRIVATE_PROFILE_INVALID");
        if (OperatingSystem.IsWindows()) return;
        const UnixFileMode forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        if ((File.GetUnixFileMode(path) & forbidden) != 0 || (File.GetUnixFileMode(directory.FullName) & forbidden) != 0)
            throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_PRIVATE_MODE_INVALID");
    }

    private static async Task VerifyDedicatedMarkerAsync(string connectionString, M0Settings settings)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT DB_NAME(), COUNT(*)
            FROM ops.DevelopmentFixtureMarker
            WHERE DatasetId='demo_m0'
              AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',
                    CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()
            || reader.GetString(0) != "KohamiM0"
            || reader.GetInt32(1) != 1
            || !string.Equals(settings.DatasetId, "demo_m0", StringComparison.Ordinal))
            throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_FIXTURE_MARKER_INVALID");
        Console.WriteLine("PASS dedicated SQL marker verified before synthetic acceptance writes");
    }

    private static async Task<bool> ReadMaintenanceAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", connection);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private static async Task InsertSyntheticMembersAsync(string connectionString, IReadOnlyDictionary<string, string> members)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            foreach (var (userId, email) in members)
            {
                await using var command = new SqlCommand("""
                    INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion)
                    VALUES(@user,@email,REPLICATE(N'x',100),1,0,'Active',1)
                    """, connection, transaction);
                command.Parameters.AddWithValue("@user", userId);
                command.Parameters.AddWithValue("@email", email);
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CreditAsync(string connectionString, string userId, string sourceId, long amountTwd)
    {
        var result = await AttemptCreditAsync(connectionString, userId, sourceId, amountTwd);
        if (!result.Committed || result.ReplayRejected)
            throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_INITIAL_CREDIT_FAILED");
    }

    private static async Task<CreditAttempt> AttemptCreditAsync(string connectionString, string userId, string sourceId, long amountTwd)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(
                "EXEC m0commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd",
                connection,
                transaction);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@SourceId", sourceId);
            command.Parameters.AddWithValue("@AmountTwd", amountTwd);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return new(true, false);
        }
        catch (SqlException error) when (IsReplayRejection(error))
        {
            await transaction.RollbackAsync();
            return new(false, true);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static bool IsReplayRejection(SqlException error) =>
        error.Number == 51000
        && error.Message.Contains("WALLET_SYNTHETIC_CREDIT_REPLAY", StringComparison.Ordinal);

    private static async Task<bool> RejectInvalidSourceAsync(
        string connectionString,
        string userId,
        string sourceId,
        long amountTwd)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(
                "EXEC m0commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd",
                connection,
                transaction);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@SourceId", sourceId);
            command.Parameters.AddWithValue("@AmountTwd", amountTwd);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return false;
        }
        catch (SqlException error) when (error.Number == 51000
            && error.Message.Contains("WALLET_SYNTHETIC_SOURCE_INVALID", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<bool> RejectCurrencyMismatchAsync(
        string connectionString,
        string userId,
        string sourceId,
        long amountTwd,
        string entryId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand("""
                INSERT m0commerce.WalletLedger(Dataset,EntryId,UserId,EntryType,AmountTwd,Currency,SourceId,CreatedAt)
                VALUES('demo_m0',@entry,@user,'SyntheticCredit',@amount,'USD',@source,SYSUTCDATETIME())
                """, connection, transaction);
            command.Parameters.AddWithValue("@entry", entryId);
            command.Parameters.AddWithValue("@user", userId);
            command.Parameters.AddWithValue("@amount", amountTwd);
            command.Parameters.AddWithValue("@source", sourceId);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return false;
        }
        catch (SqlException error) when (error.Number is 547 or 2601 or 2627)
        {
            await transaction.RollbackAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<bool> RejectNonTwdCurrencyAsync(
        string connectionString,
        string userId,
        string sourceId,
        long amountTwd,
        string entryId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand("""
                INSERT m0commerce.WalletLedger(Dataset,EntryId,UserId,EntryType,AmountTwd,Currency,SourceId,CreatedAt)
                VALUES('demo_m0',@entry,@user,'SyntheticCredit',@amount,'USD',@source,SYSUTCDATETIME())
                """, connection, transaction);
            command.Parameters.AddWithValue("@entry", entryId);
            command.Parameters.AddWithValue("@user", userId);
            command.Parameters.AddWithValue("@amount", amountTwd);
            command.Parameters.AddWithValue("@source", sourceId);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return false;
        }
        catch (SqlException error) when (error.Number == 547)
        {
            await transaction.RollbackAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<WalletState> ReadWalletStateAsync(string connectionString, string userId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT
                CASE WHEN EXISTS(SELECT 1 FROM m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@user) THEN 1 ELSE 0 END,
                COALESCE((SELECT AvailableTwd FROM m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@user),0),
                (SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@user)
            """, connection);
        command.Parameters.AddWithValue("@user", userId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_STATE_READ_FAILED");
        return new(reader.GetInt32(0) == 1, reader.GetInt64(1), reader.GetInt32(2));
    }

    private static async Task<int> CountSourceAsync(string connectionString, string sourceId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND EntryType='SyntheticCredit' AND SourceId=@source", connection);
        command.Parameters.AddWithValue("@source", sourceId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<SyntheticCreditRow?> ReadCreditBySourceAsync(string connectionString, string sourceId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT EntryId,UserId,AmountTwd,Currency
            FROM m0commerce.WalletLedger
            WHERE Dataset='demo_m0' AND EntryType='SyntheticCredit' AND SourceId=@source
            """, connection);
        command.Parameters.AddWithValue("@source", sourceId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var result = new SyntheticCreditRow(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3));
        if (await reader.ReadAsync()) throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_SOURCE_NOT_UNIQUE");
        return result;
    }

    private static async Task<int> CountNonTwdRowsAsync(string connectionString, string userId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@user AND Currency<>'TWD'", connection);
        command.Parameters.AddWithValue("@user", userId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task DebitAsync(string connectionString, string userId, string entryId, string sourceId, long amountTwd)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand("""
                DECLARE @result int, @balance bigint;
                EXEC @result=m0commerce.wallet_debit
                    @UserId=@user,@EntryId=@entry,@SourceId=@source,@AmountTwd=@amount,
                    @CreatedAt=@now,@AvailableTwd=@balance OUTPUT;
                SELECT @result,@balance;
                """, connection, transaction);
            command.Parameters.AddWithValue("@user", userId);
            command.Parameters.AddWithValue("@entry", entryId);
            command.Parameters.AddWithValue("@source", sourceId);
            command.Parameters.AddWithValue("@amount", amountTwd);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetInt32(0) != 1 || reader.GetInt64(1) < 0)
                throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_DEBIT_FAILED");
            await reader.CloseAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<long> ReadLedgerProjectionAsync(string connectionString, string userId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT COALESCE(SUM(CASE EntryType
                WHEN 'SyntheticCredit' THEN AmountTwd
                WHEN 'CheckoutDebit' THEN -AmountTwd
                ELSE 0 END),0)
            FROM m0commerce.WalletLedger
            WHERE Dataset='demo_m0' AND UserId=@user
              AND EntryType IN ('SyntheticCredit','CheckoutDebit')
            """, connection);
        command.Parameters.AddWithValue("@user", userId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task SetProjectionForTestAsync(string connectionString, string userId, long amountTwd)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            UPDATE m0commerce.WalletAccounts
            SET AvailableTwd=@amount,Version=Version+1,UpdatedAt=SYSUTCDATETIME()
            WHERE Dataset='demo_m0' AND UserId=@user
            """, connection);
        command.Parameters.AddWithValue("@amount", amountTwd);
        command.Parameters.AddWithValue("@user", userId);
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_PROJECTION_SEED_FAILED");
    }

    private static async Task RebuildProjectionInFixtureAsync(string connectionString, string userId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand("""
                ;WITH LedgerProjection AS
                (
                    SELECT Dataset,UserId,
                        SUM(CASE EntryType
                            WHEN 'SyntheticCredit' THEN AmountTwd
                            WHEN 'CheckoutDebit' THEN -AmountTwd
                            ELSE 0 END) AS AvailableTwd
                    FROM m0commerce.WalletLedger
                    WHERE Dataset='demo_m0' AND UserId=@user
                      AND EntryType IN ('SyntheticCredit','CheckoutDebit')
                    GROUP BY Dataset,UserId
                )
                UPDATE account
                SET AvailableTwd=projection.AvailableTwd,
                    Version=Version+1,
                    UpdatedAt=SYSUTCDATETIME()
                FROM m0commerce.WalletAccounts AS account
                JOIN LedgerProjection AS projection
                  ON projection.Dataset=account.Dataset AND projection.UserId=account.UserId
                WHERE account.Dataset='demo_m0' AND account.UserId=@user
                  AND projection.AvailableTwd>=0
                """, connection, transaction);
            command.Parameters.AddWithValue("@user", userId);
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_PROJECTION_REBUILD_FAILED");
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CleanupAsync(string connectionString, IReadOnlyDictionary<string, string> members)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            foreach (var userId in members.Keys)
            {
                await using var command = new SqlCommand("""
                    DELETE m0.SecurityAudit WHERE UserId=@user AND Action='SyntheticWalletCredit';
                    DELETE m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@user;
                    DELETE m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@user;
                    DELETE m0.Users WHERE UserId=@user AND Email=@email;
                    """, connection, transaction);
                command.Parameters.AddWithValue("@user", userId);
                command.Parameters.AddWithValue("@email", members[userId]);
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        foreach (var userId in members.Keys)
        {
            await using var verify = new SqlCommand("""
                SELECT
                    (SELECT COUNT(*) FROM m0.Users WHERE UserId=@user)
                    +(SELECT COUNT(*) FROM m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@user)
                    +(SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@user)
                    +(SELECT COUNT(*) FROM m0.SecurityAudit WHERE UserId=@user)
                """, connection);
            verify.Parameters.AddWithValue("@user", userId);
            if (Convert.ToInt32(await verify.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("WALLET_SYNTHETIC_ACCEPTANCE_CLEANUP_INCOMPLETE");
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }
}
