using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

/// <summary>Dedicated SQL setup and readback for the Issue25 Analyst reports scope slice.</summary>
public static class Issue25AnalystReportsAcceptance
{
    private sealed record BusinessSnapshot(int Inventory, int Orders, int Collections, int Returns,
        int RefundCases, int InventoryMovements, int Idempotency, int Jobs);

    private sealed record RunState(string UserId, string Email, string Password, string GrantId,
        string WrongScopeId, BusinessSnapshot Snapshot);

    private static readonly Regex StateId = new("^issue25-analyst-[a-f0-9]{32}$", RegexOptions.CultureInvariant);

    public static async Task SeedAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: false);
        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await RequireMaintenanceDisabledAsync(connection);

        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var state = new RunState("issue25-analyst-user-" + suffix,
            "issue25-analyst-" + suffix + "@example.test",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            "issue25-analyst-grant-" + suffix,
            "issue25-analyst-other-" + suffix,
            await ReadBusinessSnapshotAsync(connection));

        var fullStatePath = Path.GetFullPath(statePath);
        await WriteStateAsync(fullStatePath, state);
        var committed = false;
        try
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await using (var guard = new SqlCommand("EXEC identity_access.security_lock; IF EXISTS(SELECT 1 FROM identity_access.SecurityState WHERE Id=1 AND Maintenance=1) THROW 51000,'ISSUE25_ANALYST_MAINTENANCE_ACTIVE',1", connection, transaction))
                await guard.ExecuteNonQueryAsync();
            await EnsureKeysAvailableAsync(connection, transaction, state);
            await using (var insert = new SqlCommand("""
                INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion)
                VALUES(@user,@email,@hash,1,0,'Active',1);
                INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,Version,GrantedBy,Reason)
                VALUES(@grant,@user,'Analyst','reports.read','Report','operations',SYSDATETIMEOFFSET(),DATEADD(day,1,SYSDATETIMEOFFSET()),1,'issue25-synthetic-fixture','Dedicated Issue25 Analyst Report:operations scope acceptance');
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue("@user", state.UserId);
                insert.Parameters.AddWithValue("@email", state.Email);
                insert.Parameters.AddWithValue("@hash", new PasswordHasher<string>().HashPassword(state.Email, state.Password));
                insert.Parameters.AddWithValue("@grant", state.GrantId);
                await insert.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            committed = true;
            await VerifyGrantAsync(connection, state, "operations", revoked: false);
            await VerifyNoSessionAsync(connection, state);
            Console.WriteLine("PASS Issue25 synthetic Analyst seeded with only reports.read on Report:operations");
        }
        catch
        {
            if (committed)
            {
                try { await CleanupAsync(configPath, fullStatePath); }
                catch { throw new InvalidOperationException("ISSUE25_ANALYST_SEED_CLEANUP_FAILED_STATE_PRESERVED"); }
            }
            else File.Delete(fullStatePath);
            throw;
        }
    }

    public static Task OpenMaintenanceWindowAsync(string configPath, string statePath) =>
        Issue11MaintenanceWindow.OpenAsync(configPath, statePath);

    public static Task RestoreMaintenanceWindowAsync(string configPath, string statePath) =>
        Issue11MaintenanceWindow.RestoreAsync(configPath, statePath);

    public static async Task VerifyAuthorizedAsync(string configPath, string statePath)
    {
        var (connection, state) = await OpenAsync(configPath, statePath);
        await using (connection)
        {
            await VerifyGrantAsync(connection, state, "operations", revoked: false);
            await VerifyOneActiveSessionAsync(connection, state);
            await VerifyAuditAsync(connection, state, authorized: 1, denied: 0);
            await VerifyBusinessSnapshotAsync(connection, state);
        }
    }

    public static async Task SetWrongScopeAsync(string configPath, string statePath)
    {
        var (connection, state) = await OpenAsync(configPath, statePath);
        await using (connection)
        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
        {
            await LockSecurityAsync(connection, transaction);
            await using var update = new SqlCommand("UPDATE identity_access.Grants SET ScopeId=@wrong,Version=Version+1 WHERE GrantId=@grant AND UserId=@user AND Role='Analyst' AND Permission='reports.read' AND ScopeKind='Report' AND ScopeId='operations' AND RevokedAt IS NULL; IF @@ROWCOUNT<>1 THROW 51000,'ISSUE25_ANALYST_SCOPE_CHANGE_FAILED',1", connection, transaction);
            update.Parameters.AddWithValue("@wrong", state.WrongScopeId);
            AddActor(update, state);
            await update.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
    }

    public static async Task VerifyWrongScopeAsync(string configPath, string statePath)
    {
        var (connection, state) = await OpenAsync(configPath, statePath);
        await using (connection)
        {
            await VerifyGrantAsync(connection, state, state.WrongScopeId, revoked: false);
            await VerifyOneActiveSessionAsync(connection, state);
            await VerifyAuditAsync(connection, state, authorized: 1, denied: 1);
            await VerifyBusinessSnapshotAsync(connection, state);
        }
    }

    public static async Task RestoreScopeAsync(string configPath, string statePath)
    {
        var (connection, state) = await OpenAsync(configPath, statePath);
        await using (connection)
        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
        {
            await LockSecurityAsync(connection, transaction);
            await using var update = new SqlCommand("UPDATE identity_access.Grants SET ScopeId='operations',Version=Version+1 WHERE GrantId=@grant AND UserId=@user AND Role='Analyst' AND Permission='reports.read' AND ScopeKind='Report' AND ScopeId=@wrong AND RevokedAt IS NULL; IF @@ROWCOUNT<>1 THROW 51000,'ISSUE25_ANALYST_SCOPE_RESTORE_FAILED',1", connection, transaction);
            update.Parameters.AddWithValue("@wrong", state.WrongScopeId);
            AddActor(update, state);
            await update.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
    }

    public static async Task RevokeGrantAsync(string configPath, string statePath)
    {
        var (connection, state) = await OpenAsync(configPath, statePath);
        await using (connection)
        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
        {
            await LockSecurityAsync(connection, transaction);
            await using var update = new SqlCommand("UPDATE identity_access.Grants SET RevokedAt=SYSDATETIMEOFFSET(),Version=Version+1 WHERE GrantId=@grant AND UserId=@user AND Role='Analyst' AND Permission='reports.read' AND ScopeKind='Report' AND ScopeId='operations' AND RevokedAt IS NULL; IF @@ROWCOUNT<>1 THROW 51000,'ISSUE25_ANALYST_GRANT_REVOCATION_FAILED',1", connection, transaction);
            AddActor(update, state);
            await update.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
    }

    public static async Task VerifyRevokedAsync(string configPath, string statePath)
    {
        var (connection, state) = await OpenAsync(configPath, statePath);
        await using (connection)
        {
            await VerifyGrantAsync(connection, state, "operations", revoked: true);
            await VerifyOneActiveSessionAsync(connection, state);
            await VerifyAuditAsync(connection, state, authorized: 1, denied: 2);
            await VerifyBusinessSnapshotAsync(connection, state);
        }
    }

    public static async Task CleanupAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: true);
        var state = await ReadStateAsync(statePath);
        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
        {
            await LockSecurityAsync(connection, transaction);
            await using var cleanup = new SqlCommand("""
                IF (SELECT COUNT(*) FROM identity_access.Users WHERE UserId=@user AND Email=@email AND Status='Active')<>1
                   OR (SELECT COUNT(*) FROM identity_access.Grants WHERE GrantId=@grant AND UserId=@user AND Role='Analyst' AND Permission='reports.read' AND ScopeKind='Report' AND ScopeId IN('operations',@wrong))<>1
                   THROW 51000,'ISSUE25_ANALYST_CLEANUP_OWNERSHIP_MISMATCH',1;
                DELETE FROM identity_access.Sessions WHERE UserId=@user;
                DELETE FROM commerce.ReportAccessAudit WHERE ActorUserId=@user;
                DELETE FROM identity_access.SecurityAudit WHERE UserId=@user;
                DELETE FROM identity_access.Grants WHERE GrantId=@grant AND UserId=@user AND Role='Analyst' AND Permission='reports.read' AND ScopeKind='Report' AND ScopeId IN('operations',@wrong);
                DELETE FROM identity_access.Users WHERE UserId=@user AND Email=@email;
                """, connection, transaction);
            AddActor(cleanup, state);
            cleanup.Parameters.AddWithValue("@email", state.Email);
            cleanup.Parameters.AddWithValue("@wrong", state.WrongScopeId);
            await cleanup.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        await VerifyRunRowsRemovedAsync(connection, state);
        await VerifyBusinessSnapshotAsync(connection, state);
        File.Delete(Path.GetFullPath(statePath));
        Console.WriteLine("PASS Issue25 Analyst user/grant/session/audit rows cleaned and verified");
    }

    private static async Task<(SqlConnection Connection, RunState State)> OpenAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: true);
        var state = await ReadStateAsync(statePath);
        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync();
            await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
            return (connection, state);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static async Task VerifyGrantAsync(SqlConnection connection, RunState state, string scopeId, bool revoked)
    {
        await using var command = new SqlCommand("""
            SELECT COUNT(*),SUM(CASE WHEN RevokedAt IS NOT NULL THEN 1 ELSE 0 END),
              SUM(CASE WHEN RevokedAt IS NULL AND ValidFrom<=SYSDATETIMEOFFSET() AND ValidUntil>SYSDATETIMEOFFSET() THEN 1 ELSE 0 END),
              (SELECT COUNT(*) FROM identity_access.Users WHERE UserId=@user AND Email=@email AND Status='Active' AND EmailConfirmed=1 AND SecurityHold=0 AND SecurityVersion=1)
            FROM identity_access.Grants
            WHERE GrantId=@grant AND UserId=@user AND Role='Analyst' AND Permission='reports.read' AND ScopeKind='Report' AND ScopeId=@scope;
            """, connection);
        AddActor(command, state);
        command.Parameters.AddWithValue("@email", state.Email);
        command.Parameters.AddWithValue("@scope", scopeId);
        await using var reader = await command.ExecuteReaderAsync();
        var activeExpected = revoked ? 0 : 1;
        if (!await reader.ReadAsync() || reader.GetInt32(0) != 1 || reader.GetInt32(1) != (revoked ? 1 : 0)
            || reader.GetInt32(2) != activeExpected || reader.GetInt32(3) != 1)
            throw new InvalidOperationException("ISSUE25_ANALYST_GRANT_READBACK_FAILED");
    }

    private static async Task VerifyNoSessionAsync(SqlConnection connection, RunState state)
    {
        await using var command = new SqlCommand("SELECT COUNT(*) FROM identity_access.Sessions WHERE UserId=@user", connection);
        AddActor(command, state);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("ISSUE25_ANALYST_PRELOGIN_SESSION_EXISTS");
    }

    private static async Task VerifyOneActiveSessionAsync(SqlConnection connection, RunState state)
    {
        await using var command = new SqlCommand("SELECT COUNT(*),SUM(CASE WHEN RevokedAt IS NULL AND ExpiresAt>SYSDATETIMEOFFSET() AND AbsoluteUntil>SYSDATETIMEOFFSET() AND Stage='AdminAuthenticated' THEN 1 ELSE 0 END) FROM identity_access.Sessions WHERE UserId=@user", connection);
        AddActor(command, state);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetInt32(0) != 1 || reader.GetInt32(1) != 1)
            throw new InvalidOperationException("ISSUE25_ANALYST_SESSION_READBACK_FAILED");
    }

    private static async Task VerifyAuditAsync(SqlConnection connection, RunState state, int authorized, int denied)
    {
        await using var command = new SqlCommand("SELECT SUM(CASE WHEN AuthorizationOutcome='Authorized' THEN 1 ELSE 0 END),SUM(CASE WHEN AuthorizationOutcome='Denied' THEN 1 ELSE 0 END) FROM commerce.ReportAccessAudit WHERE Dataset='kohami_local' AND ActorUserId=@user", connection);
        AddActor(command, state);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || Convert.ToInt32(reader.IsDBNull(0) ? 0 : reader.GetValue(0)) != authorized
            || Convert.ToInt32(reader.IsDBNull(1) ? 0 : reader.GetValue(1)) != denied)
            throw new InvalidOperationException("ISSUE25_ANALYST_REPORT_AUDIT_READBACK_FAILED");
    }

    private static async Task<BusinessSnapshot> ReadBusinessSnapshotAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM commerce.Inventory WHERE Dataset='kohami_local'),
              (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order'),
              (SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local'),
              (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Return'),
              (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='RefundCase'),
              (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='InventoryMovement'),
              (SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local'),
              (SELECT COUNT(*) FROM commerce.Jobs WHERE Dataset='kohami_local');
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE25_ANALYST_BUSINESS_SNAPSHOT_MISSING");
        return new BusinessSnapshot(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
            reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7));
    }

    private static async Task VerifyBusinessSnapshotAsync(SqlConnection connection, RunState state)
    {
        if (await ReadBusinessSnapshotAsync(connection) != state.Snapshot)
            throw new InvalidOperationException("ISSUE25_ANALYST_REPORT_CHANGED_BUSINESS_STATE");
    }

    private static async Task VerifyRunRowsRemovedAsync(SqlConnection connection, RunState state)
    {
        await using var command = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM identity_access.Users WHERE UserId=@user OR Email=@email),
              (SELECT COUNT(*) FROM identity_access.Grants WHERE GrantId=@grant OR UserId=@user),
              (SELECT COUNT(*) FROM identity_access.Sessions WHERE UserId=@user),
              (SELECT COUNT(*) FROM identity_access.SecurityAudit WHERE UserId=@user),
              (SELECT COUNT(*) FROM commerce.ReportAccessAudit WHERE Dataset='kohami_local' AND ActorUserId=@user);
            """, connection);
        AddActor(command, state);
        command.Parameters.AddWithValue("@email", state.Email);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || Enumerable.Range(0, 5).Any(index => reader.GetInt32(index) != 0))
            throw new InvalidOperationException("ISSUE25_ANALYST_CLEANUP_READBACK_FAILED");
    }

    private static async Task EnsureKeysAvailableAsync(SqlConnection connection, SqlTransaction transaction, RunState state)
    {
        await using var command = new SqlCommand("SELECT (SELECT COUNT(*) FROM identity_access.Users WHERE UserId=@user OR Email=@email)+(SELECT COUNT(*) FROM identity_access.Grants WHERE GrantId=@grant)", connection, transaction);
        AddActor(command, state);
        command.Parameters.AddWithValue("@email", state.Email);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("ISSUE25_ANALYST_SYNTHETIC_KEYS_ALREADY_EXIST");
    }

    private static async Task RequireMaintenanceDisabledAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1", connection);
        if (!Equals(await command.ExecuteScalarAsync(), false)) throw new InvalidOperationException("ISSUE25_ANALYST_FIXTURE_MAINTENANCE_ACTIVE");
    }

    private static async Task LockSecurityAsync(SqlConnection connection, SqlTransaction transaction)
    {
        await using var command = new SqlCommand("EXEC identity_access.security_lock; IF EXISTS(SELECT 1 FROM identity_access.SecurityState WHERE Id=1 AND Maintenance=1) THROW 51000,'ISSUE25_ANALYST_MAINTENANCE_ACTIVE',1", connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddActor(SqlCommand command, RunState state)
    {
        command.Parameters.AddWithValue("@user", state.UserId);
        command.Parameters.AddWithValue("@grant", state.GrantId);
    }

    private static async Task WriteStateAsync(string path, RunState state)
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await JsonSerializer.SerializeAsync(file, state);
        await file.WriteAsync("\n"u8.ToArray());
    }

    private static async Task<RunState> ReadStateAsync(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.LinkTarget is not null || (!OperatingSystem.IsWindows() && (info.UnixFileMode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0))
            throw new InvalidOperationException("ISSUE25_ANALYST_PRIVATE_STATE_UNSAFE");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var state = await JsonSerializer.DeserializeAsync<RunState>(file) ?? throw new InvalidOperationException("ISSUE25_ANALYST_PRIVATE_STATE_INVALID");
        if (!StateId.IsMatch(state.UserId.Replace("issue25-analyst-user-", "issue25-analyst-", StringComparison.Ordinal))
            || !state.Email.EndsWith("@example.test", StringComparison.Ordinal)
            || !state.UserId.StartsWith("issue25-analyst-user-", StringComparison.Ordinal)
            || !state.GrantId.StartsWith("issue25-analyst-grant-", StringComparison.Ordinal)
            || !state.WrongScopeId.StartsWith("issue25-analyst-other-", StringComparison.Ordinal))
            throw new InvalidOperationException("ISSUE25_ANALYST_PRIVATE_STATE_INVALID");
        return state;
    }

    private static void ValidateStatePath(string configPath, string statePath, bool mustExist)
    {
        var config = Path.GetFullPath(configPath);
        var state = Path.GetFullPath(statePath);
        if (Path.GetDirectoryName(config) != Path.GetDirectoryName(state) || Path.GetFileName(state) != "issue25-analyst-reports-private.json")
            throw new InvalidOperationException("ISSUE25_ANALYST_STATE_PATH_INVALID");
        var info = new FileInfo(state);
        if (info.LinkTarget is not null || info.Exists != mustExist) throw new InvalidOperationException("ISSUE25_ANALYST_STATE_PATH_INVALID");
    }
}
