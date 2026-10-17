using Kohami.M0.Commerce;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

public static class Issue23DataLifecycleSqlAcceptance {
    static void Check(bool condition, string name) {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }

    public static async Task RunAsync(CommerceStore store, string adminConnectionString) {
        await VerifyAccountActivitySource(adminConnectionString);
        var now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var actor = "issue23_retention_" + Guid.NewGuid().ToString("N");
        var accountKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(actor))).ToLowerInvariant();
        var reportOld = now.AddYears(-1).AddTicks(-1);
        var reportBoundary = now.AddYears(-1);
        var securityOld = now.AddYears(-1).AddTicks(-1);
        var securityBoundary = now.AddYears(-1);
        var loginOld = now.AddDays(-90).AddTicks(-1);
        var loginBoundary = now.AddDays(-90);
        await using (var connection = new SqlConnection(adminConnectionString)) {
            await connection.OpenAsync();
            await using var command = new SqlCommand("""
                INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES(@actor,'Issue23RetentionOld',@securityOld,1),(@actor,'Issue23RetentionBoundary',@securityBoundary,1);
                INSERT m0.AuthFailures(AccountKey,BrowserKey,IpKey,At) VALUES(@account,@browser,@ip,@loginOld),(@account,@browser,@ip,@loginBoundary);
                INSERT m0commerce.ReportAccessAudit(Dataset,ActorUserId,RequestedAt,AuthorizationOutcome) VALUES('demo_m0',@actor,@reportOld,'Denied'),('demo_m0',@actor,@reportBoundary,'Denied');
                """, connection);
            command.Parameters.AddWithValue("@actor", actor);
            command.Parameters.AddWithValue("@securityOld", securityOld);
            command.Parameters.AddWithValue("@securityBoundary", securityBoundary);
            command.Parameters.AddWithValue("@account", accountKey);
            command.Parameters.AddWithValue("@browser", accountKey);
            command.Parameters.AddWithValue("@ip", accountKey);
            command.Parameters.AddWithValue("@loginOld", loginOld);
            command.Parameters.AddWithValue("@loginBoundary", loginBoundary);
            command.Parameters.AddWithValue("@reportOld", reportOld);
            command.Parameters.AddWithValue("@reportBoundary", reportBoundary);
            await command.ExecuteNonQueryAsync();
        }

        try {
            var worker = new MockWorker(store, TimeProvider.System, NullLogger<MockWorker>.Instance);
            await worker.PurgeExpiredLogs(now);
            await using var connection = new SqlConnection(adminConnectionString);
            await connection.OpenAsync();
            async Task<int> Count(string sql) {
                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@actor", actor);
                command.Parameters.AddWithValue("@account", accountKey);
                command.Parameters.AddWithValue("@cutoff90", now.AddDays(-90));
                command.Parameters.AddWithValue("@cutoff365", now.AddYears(-1));
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
            Check(await Count("SELECT COUNT(*) FROM m0.AuthFailures WHERE AccountKey=@account AND At<@cutoff90") == 0, "login failure older than 90 days is purged");
            Check(await Count("SELECT COUNT(*) FROM m0.AuthFailures WHERE AccountKey=@account") == 1, "90-day login failure cutoff is exclusive");
            Check(await Count("SELECT COUNT(*) FROM m0.SecurityAudit WHERE UserId=@actor AND Action LIKE 'Issue23Retention%'") == 1, "one-year security audit cutoff preserves the boundary row");
            Check(await Count("SELECT COUNT(*) FROM m0commerce.ReportAccessAudit WHERE ActorUserId=@actor") == 1, "one-year report audit cutoff preserves the boundary row");
            Check(await Count("SELECT COUNT(*) FROM m0.SecurityAudit WHERE UserId=@actor AND Action='Issue23RetentionOld'") == 0, "security audit older than one year is purged");
            Check(await Count("SELECT COUNT(*) FROM m0commerce.ReportAccessAudit WHERE ActorUserId=@actor AND RequestedAt<@cutoff365") == 0, "report access audit older than one year is purged");
        }
        finally {
            await using var cleanup = new SqlConnection(adminConnectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE m0.AuthFailures WHERE AccountKey=@account; DELETE m0.SecurityAudit WHERE UserId=@actor AND Action LIKE 'Issue23Retention%'; DELETE m0commerce.ReportAccessAudit WHERE ActorUserId=@actor", cleanup);
            command.Parameters.AddWithValue("@actor", actor);
            command.Parameters.AddWithValue("@account", accountKey);
            await command.ExecuteNonQueryAsync();
        }
    }

    static async Task VerifyAccountActivitySource(string adminConnectionString) {
        var userId = "issue23_activity_" + Guid.NewGuid().ToString("N");
        var firstSessionId = "session_issue23_" + Guid.NewGuid().ToString("N");
        var revokedSessionId = "session_issue23_" + Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await using var connection = new SqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using (var seed = new SqlCommand("INSERT m0.Users(UserId,Email,PasswordHash,Status,LastActivityAt) VALUES(@user,@email,'synthetic','SetupOnly',NULL)", connection)) {
            seed.Parameters.AddWithValue("@user", userId);
            seed.Parameters.AddWithValue("@email", userId + "@example.test");
            await seed.ExecuteNonQueryAsync();
        }

        try {
            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync()) {
                await using var session = new SqlCommand("""
                    EXEC m0.security_lock;
                    EXEC m0.security_create_session @UserId=@user,@SecurityVersion=1,@SessionId=@session,@Nonce='issue23_nonce',@Now=@now;
                    EXEC m0.security_touch_session @SessionId=@session,@Now=@now;
                    """, connection, transaction);
                session.Parameters.AddWithValue("@user", userId);
                session.Parameters.AddWithValue("@session", firstSessionId);
                session.Parameters.AddWithValue("@now", now);
                await session.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }

            async Task<DateTimeOffset?> ActivityAt() {
                await using var query = new SqlCommand("SELECT LastActivityAt FROM m0.Users WHERE UserId=@user", connection);
                query.Parameters.AddWithValue("@user", userId);
                var value = await query.ExecuteScalarAsync();
                return value is DateTimeOffset activity ? activity : null;
            }
            Check(await ActivityAt() == now, "successful setup-only sign-in records UTC account activity");

            async Task Touch(string sessionId, DateTimeOffset at) {
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
                try {
                    await using var command = new SqlCommand("EXEC m0.security_lock; EXEC m0.security_touch_session @SessionId=@session,@Now=@now", connection, transaction);
                    command.Parameters.AddWithValue("@session", sessionId);
                    command.Parameters.AddWithValue("@now", at);
                    await command.ExecuteNonQueryAsync();
                    await transaction.CommitAsync();
                } catch {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            await Touch(firstSessionId, now.AddMinutes(5));
            Check(await ActivityAt() == now.AddMinutes(5), "valid authenticated session refresh advances account activity");
            await Touch(firstSessionId, now.AddMinutes(4));
            Check(await ActivityAt() == now.AddMinutes(5), "older session timestamp cannot move account activity backwards");

            await using (var expire = new SqlCommand("UPDATE m0.Sessions SET ExpiresAt=@expired WHERE SessionId=@session", connection)) {
                expire.Parameters.AddWithValue("@expired", now.AddMinutes(20));
                expire.Parameters.AddWithValue("@session", firstSessionId);
                await expire.ExecuteNonQueryAsync();
            }
            await Touch(firstSessionId, now.AddMinutes(21));
            Check(await ActivityAt() == now.AddMinutes(5), "expired session does not advance account activity");

            await using (var seedRevoked = new SqlCommand("INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,AuthenticatedAt,RevokedAt) VALUES(@session,@user,1,'AdminAuthenticated',@expires,@absolute,'issue23_nonce',@authenticated,@revoked)", connection)) {
                seedRevoked.Parameters.AddWithValue("@session", revokedSessionId);
                seedRevoked.Parameters.AddWithValue("@user", userId);
                seedRevoked.Parameters.AddWithValue("@expires", now.AddHours(1));
                seedRevoked.Parameters.AddWithValue("@absolute", now.AddHours(8));
                seedRevoked.Parameters.AddWithValue("@authenticated", now);
                seedRevoked.Parameters.AddWithValue("@revoked", now.AddMinutes(6));
                await seedRevoked.ExecuteNonQueryAsync();
            }
            await Touch(revokedSessionId, now.AddMinutes(7));
            Check(await ActivityAt() == now.AddMinutes(5), "revoked session does not advance account activity");
        } finally {
            await using var cleanup = new SqlCommand("DELETE m0.Sessions WHERE UserId=@user; DELETE m0.Users WHERE UserId=@user", connection);
            cleanup.Parameters.AddWithValue("@user", userId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
