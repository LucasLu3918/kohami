using Kohami.M0.Commerce;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;

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
            await VerifyAftercareRetention(store, connection, now, actor);
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

    static async Task VerifyAftercareRetention(CommerceStore store, SqlConnection connection, DateTimeOffset now, string token) {
        var deliveryCutoff = now.AddDays(-180);
        var careCutoff = now.AddYears(-1);
        var oldShipmentId = "shipment_issue23_old_" + token;
        var boundaryShipmentId = "shipment_issue23_boundary_" + token;
        var activeShipmentId = "shipment_issue23_active_" + token;
        var oldRejectedId = "return_issue23_old_" + token;
        var linkedRejectedId = "return_issue23_linked_" + token;
        var inspectedId = "return_issue23_inspected_" + token;
        var financialId = "refund_issue23_financial_" + token;
        string Shipment(string id, string state, DateTimeOffset terminalAt) => new JsonObject {
            ["id"] = id, ["orderId"] = "order_" + token, ["mode"] = "mock", ["state"] = state,
            ["version"] = "before", ["createdAt"] = terminalAt.AddDays(-3).ToString("O"),
            ["displayNotice"] = "synthetic detail",
            ["history"] = new JsonArray(
                new JsonObject { ["state"] = "Pending", ["at"] = terminalAt.AddDays(-3).ToString("O"), ["actor"] = "staff_" + token, ["eventId"] = "event_" + token },
                new JsonObject { ["state"] = state, ["at"] = terminalAt.ToString("O"), ["actor"] = "staff_" + token, ["eventId"] = "event_terminal_" + token, ["reason"] = "synthetic private reason" })
        }.ToJsonString();
        string Return(string id, string state, DateTimeOffset closedAt) => new JsonObject {
            ["id"] = id, ["orderId"] = "order_" + token, ["orderLineId"] = "line_" + token, ["skuId"] = "sku_" + token,
            ["quantity"] = 1, ["unitIds"] = new JsonArray("unit_" + token), ["reason"] = "synthetic private return reason",
            ["mode"] = "mock", ["state"] = state, ["version"] = "before", ["createdAt"] = closedAt.AddDays(-2).ToString("O"),
            ["history"] = new JsonArray(
                new JsonObject { ["state"] = "Requested", ["at"] = closedAt.AddDays(-2).ToString("O"), ["actor"] = "user_" + token },
                new JsonObject { ["state"] = state, ["at"] = closedAt.ToString("O"), ["actor"] = "staff_" + token, ["reason"] = "synthetic private rejection reason" }),
            ["inspection"] = new JsonObject { ["reason"] = "synthetic private inspection note", ["resellableQuantity"] = 0, ["quarantinedQuantity"] = 1 }
        }.ToJsonString();
        var shipmentBodyOld = Shipment(oldShipmentId, "Delivered", deliveryCutoff.AddTicks(-1));
        var shipmentBodyBoundary = Shipment(boundaryShipmentId, "Delivered", deliveryCutoff);
        var shipmentBodyActive = Shipment(activeShipmentId, "InTransit", deliveryCutoff.AddDays(-1));
        var returnBodyOld = Return(oldRejectedId, "Rejected", careCutoff.AddTicks(-1));
        var returnBodyLinked = Return(linkedRejectedId, "Rejected", careCutoff.AddDays(-1));
        var returnBodyInspected = Return(inspectedId, "Inspected", careCutoff.AddDays(-1));
        var rows = new[] {
            ("Shipment", oldShipmentId, shipmentBodyOld), ("Shipment", boundaryShipmentId, shipmentBodyBoundary), ("Shipment", activeShipmentId, shipmentBodyActive),
            ("Return", oldRejectedId, returnBodyOld), ("Return", linkedRejectedId, returnBodyLinked), ("Return", inspectedId, returnBodyInspected),
            ("RefundOperation", financialId, new JsonObject { ["id"] = financialId, ["orderId"] = "order_" + token, ["state"] = "Succeeded", ["amountTwd"] = 425 }.ToJsonString())
        };
        await using (var seed = new SqlCommand("INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('demo_m0',@kind,@id,@owner,1,@body)", connection)) {
            var kind = seed.Parameters.Add("@kind", System.Data.SqlDbType.VarChar, 40);
            var id = seed.Parameters.Add("@id", System.Data.SqlDbType.VarChar, 100);
            var owner = seed.Parameters.Add("@owner", System.Data.SqlDbType.NVarChar, 100);
            var body = seed.Parameters.Add("@body", System.Data.SqlDbType.NVarChar, -1);
            foreach (var row in rows) {
                kind.Value = row.Item1; id.Value = row.Item2; owner.Value = "owner_" + token; body.Value = row.Item3;
                await seed.ExecuteNonQueryAsync();
            }
        }
        try {
            await using (var linked = new SqlCommand("INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('demo_m0','RefundCase',@id,@owner,1,@body)", connection)) {
                linked.Parameters.AddWithValue("@id", "refundcase_issue23_" + token);
                linked.Parameters.AddWithValue("@owner", "owner_" + token);
                linked.Parameters.AddWithValue("@body", new JsonObject { ["id"] = "refundcase_issue23_" + token, ["returnId"] = linkedRejectedId, ["state"] = "Requested" }.ToJsonString());
                await linked.ExecuteNonQueryAsync();
            }
            var worker = new MockWorker(store, TimeProvider.System, NullLogger<MockWorker>.Instance);
            await worker.PurgeExpiredLogs(now);

            async Task<(string Owner, long Version, JsonObject Body)> Read(string kind, string id) {
                await using var query = new SqlCommand("SELECT OwnerId,Version,Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind=@kind AND Id=@id", connection);
                query.Parameters.AddWithValue("@kind", kind); query.Parameters.AddWithValue("@id", id);
                await using var reader = await query.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("FAILED: retained summary missing: " + id);
                return (reader.GetString(0), reader.GetInt64(1), JsonNode.Parse(reader.GetString(2))!.AsObject());
            }
            var redactedShipment = await Read("Shipment", oldShipmentId);
            Check(redactedShipment.Body["retention"]?["redactedAt"] is not null, "delivered shipment older than 180 days is redacted");
            Check(redactedShipment.Body["orderId"]?.GetValue<string>() == "order_" + token && redactedShipment.Body["state"]?.GetValue<string>() == "Delivered", "shipment order link and state summary survive redaction");
            Check(redactedShipment.Owner == "retention_redacted" && !redactedShipment.Body.ToJsonString().Contains("synthetic private", StringComparison.Ordinal) && !redactedShipment.Body.ToJsonString().Contains("staff_" + token, StringComparison.Ordinal), "shipment actor, reason and display detail are removed");
            Check((redactedShipment.Body["history"] as JsonArray)?.All(item => item is JsonObject entry && entry.Count == 2) == true, "shipment timeline retains only state and UTC time");
            var boundaryShipment = await Read("Shipment", boundaryShipmentId);
            Check(boundaryShipment.Body["retention"] is null, "shipment exactly at 180-day boundary is retained");
            var activeShipment = await Read("Shipment", activeShipmentId);
            Check(activeShipment.Body["retention"] is null, "nonterminal shipment is retained");
            var redactedReturn = await Read("Return", oldRejectedId);
            Check(redactedReturn.Body["retention"]?["redactedAt"] is not null, "rejected return older than one year is redacted");
            Check(redactedReturn.Body["orderId"]?.GetValue<string>() == "order_" + token && redactedReturn.Body["state"]?.GetValue<string>() == "Rejected" && redactedReturn.Body["quantity"]?.GetValue<int>() == 1, "return order/status/count summary survives redaction");
            Check(redactedReturn.Owner == "retention_redacted" && redactedReturn.Body["reason"] is null && redactedReturn.Body["skuId"] is null && redactedReturn.Body["unitIds"] is null && !redactedReturn.Body.ToJsonString().Contains("synthetic private", StringComparison.Ordinal), "return personal text, SKU and unit identifiers are removed");
            var linkedReturn = await Read("Return", linkedRejectedId);
            Check(linkedReturn.Body["retention"] is null, "rejected return linked to financial evidence is retained");
            var inspectedReturn = await Read("Return", inspectedId);
            Check(inspectedReturn.Body["retention"] is null, "inspected return without explicit aftercare closure is retained");
            var financial = await Read("RefundOperation", financialId);
            Check(financial.Version == 1 && financial.Body["amountTwd"]?.GetValue<int>() == 425, "financial evidence is not changed by aftercare redaction");
            var stableVersion = redactedShipment.Version;
            await worker.PurgeExpiredLogs(now.AddDays(1));
            var repeated = await Read("Shipment", oldShipmentId);
            Check(repeated.Version == stableVersion, "redaction cleanup is idempotent");
        } finally {
            await using var cleanup = new SqlCommand("DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id LIKE @prefix", connection);
            cleanup.Parameters.AddWithValue("@prefix", "%" + token);
            await cleanup.ExecuteNonQueryAsync();
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
