using Kohami.M0.Commerce;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

public static class Issue23DataLifecycleSqlAcceptance {
    static void Check(bool condition, string name) {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }

    public static async Task RunAsync(CommerceStore store, string adminConnectionString) {
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
}
