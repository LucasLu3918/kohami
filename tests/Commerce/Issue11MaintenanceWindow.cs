using System.Text.Json;
using System.Data;
using Kohami.Core;
using Microsoft.Data.SqlClient;

internal static class Issue11MaintenanceWindow
{
    private sealed record SecurityState(
        bool Maintenance,
        string? MaintenanceTargetUserId,
        long Epoch,
        bool OwnerPrepared,
        bool BootstrapActivated,
        Guid? BootstrapOperationId,
        string? BootstrapUserId);

    private const string StateFileName = "issue11-maintenance-state-private.json";

    public static async Task OpenAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: false);
        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        var fullStatePath = Path.GetFullPath(statePath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var original = await ReadStateAsync(connection, transaction, lockRow: true);
        if (!original.Maintenance)
            throw new InvalidOperationException("ISSUE11_MAINTENANCE_WINDOW_ALREADY_OPEN_OR_NOT_REQUIRED");

        var created = false;
        try
        {
            await using (var file = new FileStream(fullStatePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(fullStatePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await JsonSerializer.SerializeAsync(file, original);
                await file.WriteAsync("\n"u8.ToArray());
            }
            await using var update = new SqlCommand("""
                UPDATE identity_access.SecurityState SET Maintenance=0
                WHERE Id=1 AND Maintenance=1 AND Epoch=@epoch
                  AND OwnerPrepared=@ownerPrepared AND BootstrapActivated=@bootstrapActivated
                  AND ((MaintenanceTargetUserId=@target) OR (MaintenanceTargetUserId IS NULL AND @target IS NULL))
                  AND ((BootstrapOperationId=@bootstrapOperation) OR (BootstrapOperationId IS NULL AND @bootstrapOperation IS NULL))
                  AND ((BootstrapUserId=@bootstrapUser) OR (BootstrapUserId IS NULL AND @bootstrapUser IS NULL));
                """, connection, transaction);
            AddStateParameters(update, original);
            if (await update.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("ISSUE11_MAINTENANCE_WINDOW_OPEN_CONFLICT");
            await transaction.CommitAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            if (created) File.Delete(fullStatePath);
            throw;
        }

        var changed = await ReadStateAsync(connection, null, lockRow: false);
        if (!MatchesExceptMaintenance(original, changed) || changed.Maintenance)
            throw new InvalidOperationException("ISSUE11_MAINTENANCE_WINDOW_OPEN_READBACK_FAILED");
        Console.WriteLine("PASS Issue11 maintenance disabled only on marker-verified canonical kohami-sql-test; exact prior state saved privately");
    }

    public static async Task RestoreAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: true);
        var fullStatePath = Path.GetFullPath(statePath);
        var original = JsonSerializer.Deserialize<SecurityState>(await File.ReadAllTextAsync(fullStatePath))
            ?? throw new InvalidOperationException("ISSUE11_MAINTENANCE_STATE_INVALID");
        if (!original.Maintenance)
            throw new InvalidOperationException("ISSUE11_MAINTENANCE_STATE_UNEXPECTED_ORIGINAL_VALUE");

        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var current = await ReadStateAsync(connection, transaction, lockRow: true);
        if (!MatchesExceptMaintenance(original, current))
            throw new InvalidOperationException("ISSUE11_MAINTENANCE_WINDOW_STATE_CONFLICT");

        if (!current.Maintenance)
        {
            await using var restore = new SqlCommand("""
                UPDATE identity_access.SecurityState SET Maintenance=1
                WHERE Id=1 AND Maintenance=0 AND Epoch=@epoch
                  AND OwnerPrepared=@ownerPrepared AND BootstrapActivated=@bootstrapActivated
                  AND ((MaintenanceTargetUserId=@target) OR (MaintenanceTargetUserId IS NULL AND @target IS NULL))
                  AND ((BootstrapOperationId=@bootstrapOperation) OR (BootstrapOperationId IS NULL AND @bootstrapOperation IS NULL))
                  AND ((BootstrapUserId=@bootstrapUser) OR (BootstrapUserId IS NULL AND @bootstrapUser IS NULL));
                """, connection, transaction);
            AddStateParameters(restore, original);
            if (await restore.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("ISSUE11_MAINTENANCE_WINDOW_RESTORE_CONFLICT");
        }
        await transaction.CommitAsync();

        var restored = await ReadStateAsync(connection, null, lockRow: false);
        if (restored != original)
            throw new InvalidOperationException("ISSUE11_MAINTENANCE_WINDOW_RESTORE_READBACK_FAILED");
        File.Delete(fullStatePath);
        Console.WriteLine("PASS Issue11 fixture Maintenance and complete SecurityState restored and verified by SQL readback");
    }

    private static async Task<SecurityState> ReadStateAsync(SqlConnection connection, SqlTransaction? transaction, bool lockRow)
    {
        await using var command = new SqlCommand($"""
            SELECT Maintenance,MaintenanceTargetUserId,Epoch,OwnerPrepared,BootstrapActivated,BootstrapOperationId,BootstrapUserId
            FROM identity_access.SecurityState {(lockRow ? "WITH (UPDLOCK,HOLDLOCK)" : "")} WHERE Id=1;
            """, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE11_MAINTENANCE_STATE_MISSING");
        return new SecurityState(reader.GetBoolean(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt64(2),
            reader.GetBoolean(3), reader.GetBoolean(4), reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private static void AddStateParameters(SqlCommand command, SecurityState state)
    {
        command.Parameters.Add("@epoch", SqlDbType.BigInt).Value = state.Epoch;
        command.Parameters.Add("@ownerPrepared", SqlDbType.Bit).Value = state.OwnerPrepared;
        command.Parameters.Add("@bootstrapActivated", SqlDbType.Bit).Value = state.BootstrapActivated;
        command.Parameters.Add("@target", SqlDbType.NVarChar, 100).Value = (object?)state.MaintenanceTargetUserId ?? DBNull.Value;
        command.Parameters.Add("@bootstrapOperation", SqlDbType.UniqueIdentifier).Value = (object?)state.BootstrapOperationId ?? DBNull.Value;
        command.Parameters.Add("@bootstrapUser", SqlDbType.NVarChar, 100).Value = (object?)state.BootstrapUserId ?? DBNull.Value;
    }

    private static bool MatchesExceptMaintenance(SecurityState expected, SecurityState actual) =>
        expected with { Maintenance = actual.Maintenance } == actual;

    private static void ValidateStatePath(string configPath, string statePath, bool mustExist)
    {
        var config = new FileInfo(configPath);
        var state = new FileInfo(statePath);
        var configDirectory = Path.GetDirectoryName(config.FullName);
        if (config.LinkTarget is not null || state.Name != StateFileName || Path.GetDirectoryName(state.FullName) != configDirectory
            || state.LinkTarget is not null || state.Directory?.LinkTarget is not null
            || (mustExist ? !state.Exists : state.Exists || File.Exists(state.FullName + ".tmp")))
            throw new InvalidOperationException("ISSUE11_MAINTENANCE_STATE_PATH_INVALID");
        if (state.Exists && !OperatingSystem.IsWindows()
            && (state.UnixFileMode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0)
            throw new InvalidOperationException("ISSUE11_MAINTENANCE_STATE_PERMISSIONS_INVALID");
    }
}
