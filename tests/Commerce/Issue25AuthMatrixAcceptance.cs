using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

internal static class Issue25AuthMatrixAcceptance
{
    private const string StateFileName = "issue25-auth-matrix-private.json";
    private const int BusinessSnapshotColumnCount = 6;
    private const int CleanupReadbackColumnCount = 15;
    private sealed record OptionalAuthTables(bool RecentProofs, bool RecoveryCodes);
    private sealed record ActorState(string UserId, string Email, string Password, string AccountKey, string GrantId,
        string AllowedSku, string DeniedSku, string ProductSlug, string CatalogSku, string IdempotencyKey);

    public static async Task SeedAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: false);
        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        var state = NewState();
        var fullPath = Path.GetFullPath(statePath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using (var maintenance = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1", connection))
            if (!Equals(await maintenance.ExecuteScalarAsync(), false)) throw new InvalidOperationException("ISSUE25_FIXTURE_MAINTENANCE_ACTIVE");
        await EnsureCandidateKeysAvailableAsync(connection, state);

        var created = false;
        try
        {
            await using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await JsonSerializer.SerializeAsync(file, state);
                await file.WriteAsync("\n"u8.ToArray());
            }
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await using (var guard = new SqlCommand("EXEC identity_access.security_lock; IF EXISTS(SELECT 1 FROM identity_access.SecurityState WHERE Id=1 AND Maintenance=1) THROW 51000,'ISSUE25_MAINTENANCE_ACTIVE',1", connection, transaction))
                await guard.ExecuteNonQueryAsync();
            await using (var insert = new SqlCommand("""
                INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion)
                VALUES(@user,@email,@hash,1,0,'Active',1);
                INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,Version,GrantedBy,Reason)
                VALUES(@grant,@user,'Merchandiser','stock.adjust','Sku',@allowed,SYSDATETIMEOFFSET(),DATEADD(day,1,SYSDATETIMEOFFSET()),1,'issue25-synthetic-fixture','Dedicated Issue25 non-Owner HTTP denial acceptance');
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue("@user", state.UserId);
                insert.Parameters.AddWithValue("@email", state.Email);
                insert.Parameters.AddWithValue("@hash", new PasswordHasher<string>().HashPassword(state.Email, state.Password));
                insert.Parameters.AddWithValue("@grant", state.GrantId);
                insert.Parameters.AddWithValue("@allowed", state.AllowedSku);
                await insert.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            await VerifyActorAsync(connection, state);
            await VerifyNoBusinessSideEffectsAsync(configPath, fullPath);
            Console.WriteLine("PASS Issue25 synthetic Merchandiser seeded with one explicit Sku-scoped stock.adjust grant on marker-verified kohami-sql-test");
        }
        catch
        {
            if (created)
            {
                try { await CleanupAsync(configPath, fullPath); }
                catch { throw new InvalidOperationException("ISSUE25_SEED_CLEANUP_FAILED_STATE_PRESERVED"); }
            }
            throw;
        }
    }

    public static async Task VerifyNoBusinessSideEffectsAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: true);
        var state = await ReadStateAsync(statePath);
        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await VerifyActorAsync(connection, state);
        await using var command = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku IN(@allowed,@denied,@catalogSku)),
              (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.skuId') IN(@allowed,@denied,@catalogSku)),
              (SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@user AND OperationKey=@key),
              (SELECT COUNT(*) FROM identity_access.CatalogProducts WHERE Dataset='kohami_local' AND Slug=@slug),
              (SELECT COUNT(*) FROM identity_access.CatalogProductSkus WHERE Dataset='kohami_local' AND SkuId=@catalogSku),
              (SELECT COUNT(*) FROM identity_access.CatalogAuditEvents WHERE Dataset='kohami_local' AND JSON_VALUE(Details,'$.slug')=@slug);
            """, connection);
        AddBusinessParameters(command, state);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE25_DENIAL_SIDE_EFFECT_READBACK_MISSING");
        VerifyZeroCounts(Enumerable.Range(0, reader.FieldCount).Select(reader.GetInt32).ToArray(), BusinessSnapshotColumnCount,
            "ISSUE25_DENIAL_SIDE_EFFECT_DETECTED");
        Console.WriteLine("PASS Issue25 denial matrix left synthetic SKU inventory, movement, idempotency, and catalog rows unchanged");
    }

    public static async Task VerifySessionRevokedAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: true);
        var state = await ReadStateAsync(statePath);
        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using var command = new SqlCommand("""
            SELECT COUNT(*),
                   SUM(CASE WHEN RevokedAt IS NOT NULL THEN 1 ELSE 0 END),
                   SUM(CASE WHEN RevokedAt IS NULL AND ExpiresAt>SYSDATETIMEOFFSET() AND AbsoluteUntil>SYSDATETIMEOFFSET() THEN 1 ELSE 0 END)
            FROM identity_access.Sessions WHERE UserId=@user;
            """, connection);
        command.Parameters.AddWithValue("@user", state.UserId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetInt32(0) != 1 || reader.GetInt32(1) != 1 || reader.GetInt32(2) != 0)
            throw new InvalidOperationException("ISSUE25_SESSION_REVOCATION_READBACK_FAILED");
        Console.WriteLine("PASS Issue25 SQL readback confirms the synthetic API session is revoked and no active session remains");
    }

    public static async Task CleanupAsync(string configPath, string statePath)
    {
        ValidateStatePath(configPath, statePath, mustExist: true);
        var state = await ReadStateAsync(statePath);
        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var optionalAuthTables = await ReadOptionalAuthTablesAsync(connection, transaction);
        await using (var productGuard = new SqlCommand("""
            IF EXISTS(SELECT 1 FROM identity_access.CatalogProducts WHERE Dataset='kohami_local' AND Slug=@slug AND OwnerId<>@user)
                THROW 51000,'ISSUE25_CLEANUP_PRODUCT_OWNER_MISMATCH',1;
            IF EXISTS(SELECT 1 FROM identity_access.CatalogProductSkus WHERE Dataset='kohami_local' AND SkuId=@catalogSku AND NOT EXISTS(SELECT 1 FROM identity_access.CatalogProducts WHERE Dataset='kohami_local' AND Slug=@slug AND OwnerId=@user))
                THROW 51000,'ISSUE25_CLEANUP_SKU_OWNERSHIP_MISMATCH',1;
            """, connection, transaction))
        {
            productGuard.Parameters.AddWithValue("@slug", state.ProductSlug);
            productGuard.Parameters.AddWithValue("@catalogSku", state.CatalogSku);
            productGuard.Parameters.AddWithValue("@user", state.UserId);
            await productGuard.ExecuteNonQueryAsync();
        }
        await using (var cleanup = new SqlCommand(BuildCleanupSql(optionalAuthTables), connection, transaction))
        {
            AddBusinessParameters(cleanup, state);
            cleanup.Parameters.AddWithValue("@grant", state.GrantId);
            cleanup.Parameters.AddWithValue("@email", state.Email);
            cleanup.Parameters.AddWithValue("@accountKey", state.AccountKey);
            await cleanup.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        await VerifyRunRowsRemovedAsync(connection, state);
        File.Delete(Path.GetFullPath(statePath));
        Console.WriteLine("PASS Issue25 synthetic non-Owner records cleaned and verified by SQL readback");
    }

    private static string BuildCleanupSql(OptionalAuthTables optionalAuthTables)
    {
        var optionalDeletes = string.Join("\n", new[]
        {
            optionalAuthTables.RecentProofs ? "DELETE FROM identity_access.RecentProofs WHERE UserId=@user;" : null,
            optionalAuthTables.RecoveryCodes ? "DELETE FROM identity_access.RecoveryCodes WHERE UserId=@user;" : null,
        }.Where(statement => statement is not null));
        return $"""
            DELETE a FROM identity_access.CatalogAuditEvents a JOIN identity_access.CatalogProducts p ON p.Dataset=a.Dataset AND p.ProductId=a.ProductId WHERE p.Dataset='kohami_local' AND p.Slug=@slug AND p.OwnerId=@user;
            DELETE s FROM identity_access.CatalogProductSkus s JOIN identity_access.CatalogProducts p ON p.Dataset=s.Dataset AND p.ProductId=s.ProductId WHERE p.Dataset='kohami_local' AND p.Slug=@slug AND p.OwnerId=@user;
            DELETE p FROM identity_access.CatalogProducts p WHERE p.Dataset='kohami_local' AND p.Slug=@slug AND p.OwnerId=@user;
            DELETE i FROM commerce.Inventory i WHERE i.Dataset='kohami_local' AND i.Sku IN(@allowed,@denied,@catalogSku) AND NOT EXISTS(SELECT 1 FROM identity_access.CatalogProductSkus WHERE Dataset='kohami_local' AND SkuId=i.Sku);
            DELETE FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.skuId') IN(@allowed,@denied,@catalogSku);
            DELETE FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@user AND OperationKey=@key;
            DELETE FROM identity_access.Sessions WHERE UserId=@user;
            DELETE FROM identity_access.Challenges WHERE UserId=@user;
            DELETE FROM identity_access.SecurityAudit WHERE UserId=@user;
            {optionalDeletes}
            DELETE FROM identity_access.AuthFailures WHERE AccountKey=@accountKey;
            DELETE FROM identity_access.Grants WHERE GrantId=@grant AND UserId=@user AND Role='Merchandiser' AND Permission='stock.adjust' AND ScopeKind='Sku' AND ScopeId=@allowed;
            DELETE FROM identity_access.MemberProfiles WHERE UserId=@user;
            DELETE FROM identity_access.Users WHERE UserId=@user AND Email=@email AND Status='Active';
            """;
    }

    private static ActorState NewState()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var email = "issue25_" + suffix + "@example.test";
        var accountKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(email)));
        return new ActorState("issue25_merchandiser_" + suffix, email, password, accountKey,
            "issue25_grant_" + suffix, "issue25-allowed-" + suffix, "issue25-denied-" + suffix,
            "issue25-" + suffix, "issue25-" + suffix, "issue25-key-" + suffix);
    }

    private static async Task<ActorState> ReadStateAsync(string path) =>
        JsonSerializer.Deserialize<ActorState>(await File.ReadAllTextAsync(path))
        ?? throw new InvalidOperationException("ISSUE25_PRIVATE_STATE_INVALID");

    private static async Task VerifyActorAsync(SqlConnection connection, ActorState state)
    {
        await using var command = new SqlCommand("""
            SELECT COUNT(*),SUM(CASE WHEN g.Role='Owner' THEN 1 ELSE 0 END),SUM(CASE WHEN g.Role='Merchandiser' AND g.Permission='stock.adjust' AND g.ScopeKind='Sku' AND g.ScopeId=@allowed AND g.RevokedAt IS NULL THEN 1 ELSE 0 END)
            FROM identity_access.Users u LEFT JOIN identity_access.Grants g ON g.UserId=u.UserId
            WHERE u.UserId=@user AND u.Email=@email AND u.EmailConfirmed=1 AND u.Status='Active' AND u.SecurityHold=0;
            """, connection);
        command.Parameters.AddWithValue("@user", state.UserId);
        command.Parameters.AddWithValue("@email", state.Email);
        command.Parameters.AddWithValue("@allowed", state.AllowedSku);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetInt32(0) != 1 || reader.GetInt32(1) != 0 || reader.GetInt32(2) != 1)
            throw new InvalidOperationException("ISSUE25_SYNTHETIC_ACTOR_STATE_INVALID");
    }

    private static async Task VerifyRunRowsRemovedAsync(SqlConnection connection, ActorState state)
    {
        var optionalAuthTables = await ReadOptionalAuthTablesAsync(connection, null);
        await using var command = new SqlCommand(BuildCleanupReadbackSql(optionalAuthTables), connection);
        AddBusinessParameters(command, state);
        command.Parameters.AddWithValue("@email", state.Email);
        command.Parameters.AddWithValue("@accountKey", state.AccountKey);
        command.Parameters.AddWithValue("@grant", state.GrantId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE25_CLEANUP_READBACK_MISSING");
        VerifyZeroCounts(Enumerable.Range(0, reader.FieldCount).Select(reader.GetInt32).ToArray(), CleanupReadbackColumnCount,
            "ISSUE25_CLEANUP_READBACK_FAILED");
    }

    private static string BuildCleanupReadbackSql(OptionalAuthTables optionalAuthTables)
    {
        static string CountIfPresent(string table, bool exists) => exists
            ? $"(SELECT COUNT(*) FROM identity_access.{table} WHERE UserId=@user)"
            : "CAST(0 AS int)";
        return $"""
            SELECT (SELECT COUNT(*) FROM identity_access.Users WHERE UserId=@user OR Email=@email),
                   (SELECT COUNT(*) FROM identity_access.Grants WHERE GrantId=@grant),
                   (SELECT COUNT(*) FROM identity_access.Sessions WHERE UserId=@user),
                   (SELECT COUNT(*) FROM identity_access.Challenges WHERE UserId=@user),
                   {CountIfPresent("RecentProofs", optionalAuthTables.RecentProofs)},
                   {CountIfPresent("RecoveryCodes", optionalAuthTables.RecoveryCodes)},
                   (SELECT COUNT(*) FROM identity_access.SecurityAudit WHERE UserId=@user),
                   (SELECT COUNT(*) FROM identity_access.AuthFailures WHERE AccountKey=@accountKey),
                   (SELECT COUNT(*) FROM identity_access.MemberProfiles WHERE UserId=@user),
                   (SELECT COUNT(*) FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku IN(@allowed,@denied,@catalogSku)),
                   (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.skuId') IN(@allowed,@denied,@catalogSku)),
                   (SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@user AND OperationKey=@key),
                   (SELECT COUNT(*) FROM identity_access.CatalogProducts WHERE Dataset='kohami_local' AND Slug=@slug),
                   (SELECT COUNT(*) FROM identity_access.CatalogProductSkus WHERE Dataset='kohami_local' AND SkuId=@catalogSku),
                   (SELECT COUNT(*) FROM identity_access.CatalogAuditEvents WHERE Dataset='kohami_local' AND JSON_VALUE(Details,'$.slug')=@slug);
            """;
    }

    private static async Task<OptionalAuthTables> ReadOptionalAuthTablesAsync(SqlConnection connection, SqlTransaction? transaction)
    {
        await using var command = new SqlCommand("""
            SELECT CASE WHEN OBJECT_ID(N'identity_access.RecentProofs',N'U') IS NULL THEN CONVERT(bit,0) ELSE CONVERT(bit,1) END,
                   CASE WHEN OBJECT_ID(N'identity_access.RecoveryCodes',N'U') IS NULL THEN CONVERT(bit,0) ELSE CONVERT(bit,1) END;
            """, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE25_OPTIONAL_AUTH_SCHEMA_READBACK_MISSING");
        return new OptionalAuthTables(reader.GetBoolean(0), reader.GetBoolean(1));
    }

    private static async Task EnsureCandidateKeysAvailableAsync(SqlConnection connection, ActorState state)
    {
        await using var command = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM identity_access.Users WHERE UserId=@user OR Email=@email)+
              (SELECT COUNT(*) FROM identity_access.Grants WHERE GrantId=@grant)+
              (SELECT COUNT(*) FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku IN(@allowed,@denied,@catalogSku))+
              (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.skuId') IN(@allowed,@denied,@catalogSku))+
              (SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@user AND OperationKey=@key)+
              (SELECT COUNT(*) FROM identity_access.CatalogProducts WHERE Dataset='kohami_local' AND Slug=@slug)+
              (SELECT COUNT(*) FROM identity_access.CatalogProductSkus WHERE Dataset='kohami_local' AND SkuId IN(@allowed,@denied,@catalogSku))+
              (SELECT COUNT(*) FROM identity_access.CatalogAuditEvents WHERE Dataset='kohami_local' AND JSON_VALUE(Details,'$.slug')=@slug);
            """, connection);
        AddBusinessParameters(command, state);
        command.Parameters.AddWithValue("@email", state.Email);
        command.Parameters.AddWithValue("@grant", state.GrantId);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("ISSUE25_SYNTHETIC_CANDIDATE_KEY_COLLISION");
    }

    internal static void VerifyReadbackContract()
    {
        var optionalAbsent = new OptionalAuthTables(false, false);
        var cleanupWithoutOptionalTables = BuildCleanupSql(optionalAbsent);
        var readbackWithoutOptionalTables = BuildCleanupReadbackSql(optionalAbsent);
        var requiredCleanupTables = new[]
        {
            "identity_access.Users", "identity_access.Grants", "identity_access.Sessions",
            "identity_access.Challenges", "identity_access.SecurityAudit", "identity_access.AuthFailures",
            "identity_access.MemberProfiles", "commerce.Inventory", "commerce.Documents",
            "commerce.Idempotency", "identity_access.CatalogProducts", "identity_access.CatalogProductSkus",
            "identity_access.CatalogAuditEvents",
        };
        if (cleanupWithoutOptionalTables.Contains("identity_access.RecentProofs", StringComparison.Ordinal)
            || cleanupWithoutOptionalTables.Contains("identity_access.RecoveryCodes", StringComparison.Ordinal)
            || readbackWithoutOptionalTables.Contains("identity_access.RecentProofs", StringComparison.Ordinal)
            || readbackWithoutOptionalTables.Contains("identity_access.RecoveryCodes", StringComparison.Ordinal)
            || requiredCleanupTables.Any(table => !cleanupWithoutOptionalTables.Contains(table, StringComparison.Ordinal)
                || !readbackWithoutOptionalTables.Contains(table, StringComparison.Ordinal)))
            throw new InvalidOperationException("ISSUE25_OPTIONAL_TABLE_ABSENCE_CONTRACT_FAILED");
        var optionalPresent = new OptionalAuthTables(true, true);
        if (!BuildCleanupSql(optionalPresent).Contains("identity_access.RecentProofs", StringComparison.Ordinal)
            || !BuildCleanupSql(optionalPresent).Contains("identity_access.RecoveryCodes", StringComparison.Ordinal)
            || !BuildCleanupReadbackSql(optionalPresent).Contains("identity_access.RecentProofs", StringComparison.Ordinal)
            || !BuildCleanupReadbackSql(optionalPresent).Contains("identity_access.RecoveryCodes", StringComparison.Ordinal))
            throw new InvalidOperationException("ISSUE25_OPTIONAL_TABLE_PRESENCE_CONTRACT_FAILED");
        VerifyZeroCounts(new int[BusinessSnapshotColumnCount], BusinessSnapshotColumnCount, "ISSUE25_CONTRACT_ZERO_COUNTS_FAILED");
        VerifyZeroCounts(new int[CleanupReadbackColumnCount], CleanupReadbackColumnCount, "ISSUE25_CONTRACT_ZERO_COUNTS_FAILED");
        var lastCleanupCount = new int[CleanupReadbackColumnCount];
        lastCleanupCount[^1] = 1;
        try
        {
            VerifyZeroCounts(lastCleanupCount, CleanupReadbackColumnCount, "ISSUE25_CLEANUP_READBACK_FAILED");
            throw new InvalidOperationException("ISSUE25_CONTRACT_LAST_COLUMN_NOT_CHECKED");
        }
        catch (InvalidOperationException error) when (error.Message == "ISSUE25_CLEANUP_READBACK_FAILED") { }
        Console.WriteLine("PASS Issue25 readback contract checks every configured business and cleanup column, including the final field");
    }

    private static void VerifyZeroCounts(IReadOnlyList<int> counts, int expectedCount, string failureCode)
    {
        if (counts.Count != expectedCount || counts.Any(count => count != 0)) throw new InvalidOperationException(failureCode);
    }

    private static void AddBusinessParameters(SqlCommand command, ActorState state)
    {
        command.Parameters.AddWithValue("@allowed", state.AllowedSku);
        command.Parameters.AddWithValue("@denied", state.DeniedSku);
        command.Parameters.AddWithValue("@catalogSku", state.CatalogSku);
        command.Parameters.AddWithValue("@user", state.UserId);
        command.Parameters.AddWithValue("@key", state.IdempotencyKey);
        command.Parameters.AddWithValue("@slug", state.ProductSlug);
    }

    private static void ValidateStatePath(string configPath, string statePath, bool mustExist)
    {
        var config = new FileInfo(configPath);
        var state = new FileInfo(statePath);
        if (config.LinkTarget is not null || state.Name != StateFileName || Path.GetDirectoryName(state.FullName) != Path.GetDirectoryName(config.FullName)
            || state.LinkTarget is not null || state.Directory?.LinkTarget is not null
            || (mustExist ? !state.Exists : state.Exists))
            throw new InvalidOperationException("ISSUE25_PRIVATE_STATE_PATH_INVALID");
        if (state.Exists && !OperatingSystem.IsWindows()
            && (state.UnixFileMode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0)
            throw new InvalidOperationException("ISSUE25_PRIVATE_STATE_PERMISSIONS_INVALID");
    }
}
