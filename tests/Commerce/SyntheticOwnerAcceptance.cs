using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.WebUtilities;

internal static class SyntheticOwnerAcceptance
{
    sealed record InventorySnapshot(string Sku, bool Exists, long Price, int Stock, bool Active);
    sealed record SessionArtifact(string UserId, string BrowserId, string SessionId, string SessionCookie, string BrowserCookie, bool PreviousMaintenance, InventorySnapshot DemoSku, string? GuestReadCookie = null, string? GuestActionCookie = null, string? CommandActorId = null, string? CommandCookie = null, InventorySnapshot[]? CatalogInventory = null);
    static readonly JsonSerializerOptions ArtifactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    static (M0Settings Settings, SqlConnectionStringBuilder Admin) Load(string configPath)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = json.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean()
            || root.GetProperty("datasetId").GetString() != "demo_m0")
            throw new InvalidOperationException("DEDICATED_ISSUE06_FIXTURE_REQUIRED");
        var settings = M0Settings.Load(configPath);
        var admin = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = "localhost"
        };
        admin["Server Certificate"] = settings.SqlCertificatePath;
        var portText = admin.DataSource.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase) ? admin.DataSource["localhost,".Length..] : "";
        if (!int.TryParse(portText, out var port) || port is < 14336 or > 14399
            || !string.Equals(admin.InitialCatalog, "KohamiM0", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ISSUE06_FIXTURE_TARGET_INVALID");
        return (settings, admin);
    }

    public static async Task SeedAsync(string configPath, string artifactPath, string? resourceId)
    {
        var (settings, admin) = Load(configPath);
        if (resourceId is not null) throw new InvalidOperationException("SYNTHETIC_OWNER_SEED_ARGUMENT_INVALID");
        if (File.Exists(artifactPath) || Directory.Exists(artifactPath))
            throw new InvalidOperationException("SYNTHETIC_ARTIFACT_ALREADY_EXISTS");

        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var user = "issue06_owner_" + suffix;
        var browser = "issue06_browser_" + suffix;
        var commandActor = "issue06_command_" + suffix;
        var session = "issue06_session_" + suffix;
        var nonce = Guid.NewGuid().ToString("N");
        var expires = now.AddHours(1);
        bool previousMaintenance;
        InventorySnapshot demoSku;
        var catalogInventory = new List<InventorySnapshot>();
        await using (var inspect = new SqlConnection(admin.ConnectionString))
        {
            await inspect.OpenAsync();
            await using (var command = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", inspect))
                previousMaintenance = (bool)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("FIXTURE_STATE_INVALID"));
            await using var inventory = new SqlCommand("SELECT Sku,Price,Stock,Active FROM m0commerce.Inventory WHERE Dataset='demo_m0'", inspect);
            await using var reader = await inventory.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                catalogInventory.Add(new InventorySnapshot(reader.GetString(0), true, reader.GetInt64(1), reader.GetInt32(2), reader.GetBoolean(3)));
            demoSku = catalogInventory.SingleOrDefault(item => item.Sku == "sku_demo_03")
                ?? new InventorySnapshot("sku_demo_03", false, 650, 0, false);
            if (!catalogInventory.Any(item => item.Sku == "sku_demo_03"))
                catalogInventory.Add(demoSku);
        }

        var crypto = M0Crypto.Load(settings);
        var sessionCookie = WebEncoders.Base64UrlEncode(crypto.Sessions.CreateProtector("kohami.session").Protect(System.Text.Encoding.UTF8.GetBytes("demo_m0|" + session)));
        var browserPayload = JsonSerializer.Serialize(new { value = browser, until = expires });
        var browserCookie = WebEncoders.Base64UrlEncode(crypto.Sessions.CreateProtector("kohami.browser").Protect(System.Text.Encoding.UTF8.GetBytes(browserPayload)));
        var commandPayload = JsonSerializer.Serialize(new { value = commandActor, until = expires });
        var commandCookie = WebEncoders.Base64UrlEncode(crypto.Sessions.CreateProtector("kohami.command").Protect(System.Text.Encoding.UTF8.GetBytes(commandPayload)));
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        var artifact = new SessionArtifact(user, browser, session, sessionCookie, browserCookie, previousMaintenance, demoSku,
            CommandActorId: commandActor, CommandCookie: commandCookie, CatalogInventory: catalogInventory.ToArray());
        File.WriteAllText(artifactPath, JsonSerializer.Serialize(artifact, ArtifactJson) + "\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(artifactPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        try
        {
            await using var connection = new SqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            await using var command = new SqlCommand(@"
DECLARE @now datetimeoffset=SYSUTCDATETIME();
IF (SELECT Maintenance FROM m0.SecurityState WHERE Id=1)<>@previousMaintenance THROW 51000,'FIXTURE_STATE_CHANGED',1;
UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1;
IF EXISTS(SELECT 1 FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03')
 UPDATE m0commerce.Inventory SET Active=1,Stock=CASE WHEN Stock<1 THEN 1 ELSE Stock END WHERE Dataset='demo_m0' AND Sku='sku_demo_03';
ELSE INSERT m0commerce.Inventory(Dataset,Sku,Price,Stock,Active) VALUES('demo_m0','sku_demo_03',650,1,1);
INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion)
VALUES(@user,@email,REPLICATE(N'x',100),1,'Active',1);
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
SELECT 'issue06_grant_'+CONVERT(nvarchar(36),NEWID()),@user,'Owner',p.Permission,'Dataset','demo_m0',DATEADD(minute,-1,@now),DATEADD(hour,2,@now),'issue06-fixture','synthetic API acceptance only'
FROM (VALUES('orders.read'),('demo.control'),('shipment.manage'),('support.respond'),('return.inspect'),('refund.execute')) p(Permission);
INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,AuthenticatedAt)
VALUES(@session,@user,1,'AdminAuthenticated',DATEADD(hour,1,@now),DATEADD(hour,1,@now),@nonce,@now);", connection, transaction);
            command.Parameters.AddWithValue("@user", user);
            command.Parameters.AddWithValue("@email", user + "@example.test");
            command.Parameters.AddWithValue("@session", session);
            command.Parameters.AddWithValue("@nonce", nonce);
            command.Parameters.AddWithValue("@previousMaintenance", previousMaintenance);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            File.Delete(artifactPath);
            throw;
        }
        Console.WriteLine("PASS synthetic Issue06 Owner session prepared");
    }

    public static async Task AddGuestCapabilitiesAsync(string configPath, string artifactPath, string orderId)
    {
        var (settings, admin) = Load(configPath);
        var artifact = JsonSerializer.Deserialize<SessionArtifact>(File.ReadAllText(artifactPath), ArtifactJson)
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        if (!System.Text.RegularExpressions.Regex.IsMatch(orderId, "^order_[A-Za-z0-9_-]{1,90}$"))
            throw new InvalidOperationException("ORDER_ID_INVALID");
        var readId = "issue06_cap_read_" + Guid.NewGuid().ToString("N");
        var returnId = "issue06_cap_return_" + Guid.NewGuid().ToString("N");
        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var commandActor = artifact.CommandActorId ?? throw new InvalidOperationException("SYNTHETIC_COMMAND_ACTOR_MISSING");
        await using (var connection = new SqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"
IF NOT EXISTS(SELECT 1 FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order AND OwnerId=@owner) THROW 51000,'SYNTHETIC_ORDER_NOT_FOUND',1;
INSERT m0commerce.GuestCapabilities(Dataset,Id,OrderId,BrowserId,Intent,ExpiresAt,Consumed) VALUES('demo_m0',@read,@order,@browser,'read',@expires,0),('demo_m0',@return,@order,@browser,'return',@expires,0);", connection);
            command.Parameters.AddWithValue("@order", orderId);
            command.Parameters.AddWithValue("@owner", commandActor);
            command.Parameters.AddWithValue("@browser", artifact.BrowserId);
            command.Parameters.AddWithValue("@read", readId);
            command.Parameters.AddWithValue("@return", returnId);
            command.Parameters.AddWithValue("@expires", expires);
            await command.ExecuteNonQueryAsync();
        }
        var crypto = M0Crypto.Load(settings);
        static string Cookie(M0Crypto c, string name, string id, DateTimeOffset until)
        {
            var payload = JsonSerializer.Serialize(new { value = id, until });
            return WebEncoders.Base64UrlEncode(c.Sessions.CreateProtector(name).Protect(System.Text.Encoding.UTF8.GetBytes(payload)));
        }
        artifact = artifact with
        {
            GuestReadCookie = Cookie(crypto, "kohami.guest.read", readId, expires),
            GuestActionCookie = Cookie(crypto, "kohami.guest.action", returnId, expires),
        };
        File.WriteAllText(artifactPath, JsonSerializer.Serialize(artifact, ArtifactJson) + "\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(artifactPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Console.WriteLine("PASS synthetic guest read/return capabilities prepared for the Issue06 order");
    }

    public static async Task VerifyPriceSnapshotAsync(string configPath, string artifactPath, string checkoutId, string orderId)
    {
        var (_, admin) = Load(configPath);
        var artifact = JsonSerializer.Deserialize<SessionArtifact>(File.ReadAllText(artifactPath), ArtifactJson)
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        var commandActor = artifact.CommandActorId ?? throw new InvalidOperationException("SYNTHETIC_COMMAND_ACTOR_MISSING");
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            async Task<(string Owner, JsonObject Body)> ReadDocument(string kind, string id)
            {
                await using var command = new SqlCommand("SELECT OwnerId,Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind=@kind AND Id=@id", connection, transaction);
                command.Parameters.AddWithValue("@kind", kind);
                command.Parameters.AddWithValue("@id", id);
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("SYNTHETIC_SNAPSHOT_DOCUMENT_NOT_FOUND");
                return (reader.GetString(0), JsonNode.Parse(reader.GetString(1))?.AsObject()
                    ?? throw new InvalidOperationException("SYNTHETIC_SNAPSHOT_DOCUMENT_INVALID"));
            }

            var checkout = await ReadDocument("Checkout", checkoutId);
            var order = await ReadDocument("Order", orderId);
            if (checkout.Owner != commandActor || order.Owner != commandActor
                || checkout.Body.Text("orderId") != orderId || order.Body.Text("checkoutId") != checkoutId)
                throw new InvalidOperationException("SYNTHETIC_SNAPSHOT_OWNER_OR_LINK_INVALID");
            var checkoutSnapshot = checkout.Body["snapshot"]?.DeepClone() as JsonObject
                ?? throw new InvalidOperationException("CHECKOUT_SNAPSHOT_MISSING");
            var orderSnapshot = order.Body["snapshot"]?.DeepClone() as JsonObject
                ?? throw new InvalidOperationException("ORDER_SNAPSHOT_MISSING");
            if (!JsonNode.DeepEquals(checkoutSnapshot, orderSnapshot))
                throw new InvalidOperationException("CHECKOUT_ORDER_SNAPSHOT_MISMATCH");
            var sku = checkoutSnapshot["lines"]?.AsArray().FirstOrDefault()?.AsObject().Text("skuId")
                ?? throw new InvalidOperationException("SYNTHETIC_SNAPSHOT_SKU_MISSING");

            await using var inventory = new SqlCommand("SELECT Price FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku", connection, transaction);
            inventory.Parameters.AddWithValue("@sku", sku);
            var originalPrice = Convert.ToInt64(await inventory.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("SYNTHETIC_SNAPSHOT_INVENTORY_MISSING"));
            var changedPrice = originalPrice == 1_000_000 ? originalPrice - 1 : originalPrice + 1;
            await using (var update = new SqlCommand("UPDATE m0commerce.Inventory SET Price=@price WHERE Dataset='demo_m0' AND Sku=@sku", connection, transaction))
            {
                update.Parameters.AddWithValue("@price", changedPrice);
                update.Parameters.AddWithValue("@sku", sku);
                if (await update.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("SYNTHETIC_SNAPSHOT_PRICE_UPDATE_FAILED");
            }
            await using (var verifyPrice = new SqlCommand("SELECT Price FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku", connection, transaction))
            {
                verifyPrice.Parameters.AddWithValue("@sku", sku);
                if (Convert.ToInt64(await verifyPrice.ExecuteScalarAsync()) != changedPrice)
                    throw new InvalidOperationException("SYNTHETIC_SNAPSHOT_PRICE_CHANGE_NOT_VISIBLE");
            }
            var checkoutAfter = await ReadDocument("Checkout", checkoutId);
            var orderAfter = await ReadDocument("Order", orderId);
            if (!JsonNode.DeepEquals(checkoutSnapshot, checkoutAfter.Body["snapshot"])
                || !JsonNode.DeepEquals(orderSnapshot, orderAfter.Body["snapshot"]))
                throw new InvalidOperationException("PRICE_CHANGE_MUTATED_ORDER_SNAPSHOT");
            await transaction.RollbackAsync();
            Console.WriteLine("PASS SQL Catalog price changed inside an isolated transaction while persisted Checkout/Order price snapshots remained unchanged; fixture price rolled back");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public static async Task VerifyAftercareAsync(string configPath, string artifactPath, string orderId, string refundOperationId)
    {
        var (_, admin) = Load(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
DECLARE @confirmed int=(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order AND JSON_VALUE(Body,'$.orderState')='Confirmed' AND JSON_VALUE(Body,'$.paymentState')='Succeeded');
DECLARE @delivered int=(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Shipment' AND JSON_VALUE(Body,'$.orderId')=@order AND JSON_VALUE(Body,'$.state')='Delivered');
DECLARE @inspected int=(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Return' AND JSON_VALUE(Body,'$.orderId')=@order AND JSON_VALUE(Body,'$.state')='Inspected');
DECLARE @refundCount int=(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND Id=@refund AND JSON_VALUE(Body,'$.state') IN('Processing','Unknown'));
IF @confirmed<>1 OR @delivered<>1 OR @inspected<>1 OR @refundCount<>1 THROW 51000,'ISSUE06_API_SQL_EVIDENCE_MISSING',1;
SELECT @confirmed+@delivered+@inspected+@refundCount;", connection);
        command.Parameters.AddWithValue("@order", orderId);
        command.Parameters.AddWithValue("@refund", refundOperationId);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 4)
            throw new InvalidOperationException("ISSUE06_API_SQL_EVIDENCE_MISSING");
        Console.WriteLine("PASS SQL persisted confirmed order, delivered shipment, inspected return, and pending mock refund");
    }

    public static async Task ReportWorkerStateAsync(string configPath, string checkoutId, string paymentAttemptId, string scenarioJobId)
    {
        var (_, admin) = Load(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
SELECT COALESCE((SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='PaymentAttempt' AND Id=@payment),'MISSING'),
       COALESCE((SELECT JSON_VALUE(Body,'$.paymentState') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@checkout),'MISSING'),
       COALESCE((SELECT State FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Payment' AND TargetId=@payment),'MISSING'),
       COALESCE((SELECT TOP (1) State FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Scenario' AND JSON_VALUE(Payload,'$.targetId')=@payment ORDER BY CreatedAt DESC),'MISSING');", connection);
        command.Parameters.AddWithValue("@payment", paymentAttemptId);
        command.Parameters.AddWithValue("@checkout", checkoutId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("WORKER_STATE_UNAVAILABLE");
        var values = Enumerable.Range(0, 4).Select(reader.GetString).ToArray();
        static string Safe(string value) => value is "MISSING" or "Processing" or "Succeeded" or "Failed" or "Unknown" or "NotStarted" or "Started" or "Completed" or "Blocked" ? value : "OTHER";
        Console.WriteLine($"WORKER_STATE payment={Safe(values[0])} checkout={Safe(values[1])} payment_job={Safe(values[2])} scenario_job={Safe(values[3])}");
    }

    public static async Task VerifyPaymentOutcomeAsync(string configPath, string checkoutId, string paymentAttemptId, string expectedState, string expectedScenarioCount)
    {
        if (expectedState is not ("Failed" or "Unknown") || expectedScenarioCount is not ("0" or "1"))
            throw new InvalidOperationException("ISSUE13_PAYMENT_EXPECTATION_INVALID");
        var (_, admin) = Load(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
SELECT COALESCE((SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='PaymentAttempt' AND Id=@payment),'MISSING'),
       COALESCE((SELECT JSON_VALUE(Body,'$.paymentState') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@checkout),'MISSING'),
       COALESCE((SELECT JSON_VALUE(Body,'$.orderState') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@checkout),'MISSING'),
       COALESCE((SELECT State FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Payment' AND TargetId=@payment),'MISSING'),
       COALESCE((SELECT JSON_VALUE(Body,'$.paymentState') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=(SELECT JSON_VALUE(Body,'$.orderId') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@checkout)),'MISSING'),
       COALESCE((SELECT JSON_VALUE(Body,'$.orderState') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=(SELECT JSON_VALUE(Body,'$.orderId') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@checkout)),'MISSING'),
       (SELECT COUNT(*) FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Scenario' AND JSON_VALUE(Payload,'$.targetId')=@payment AND State='Completed'),
       (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='PaymentAttempt' AND JSON_VALUE(Body,'$.checkoutId')=@checkout),
       (SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=(SELECT JSON_VALUE(Body,'$.orderId') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@checkout));", connection);
        command.Parameters.AddWithValue("@payment", paymentAttemptId);
        command.Parameters.AddWithValue("@checkout", checkoutId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE13_PAYMENT_SQL_STATE_MISSING");
        var actualState = reader.GetString(0);
        var checkoutState = reader.GetString(1);
        var orderState = reader.GetString(2);
        var paymentJobState = reader.GetString(3);
        var orderPaymentState = reader.GetString(4);
        var orderDocumentState = reader.GetString(5);
        var scenarioCount = reader.GetInt32(6);
        var attemptCount = reader.GetInt32(7);
        var collectionCount = reader.GetInt32(8);
        if (actualState != expectedState || checkoutState != expectedState || orderState != "AwaitingPayment"
            || orderPaymentState != expectedState || orderDocumentState != "AwaitingPayment"
            || paymentJobState != "Completed" || scenarioCount != int.Parse(expectedScenarioCount)
            || attemptCount != 1 || collectionCount != 0)
            throw new InvalidOperationException("ISSUE13_PAYMENT_SQL_STATE_MISMATCH");
        Console.WriteLine($"PASS SQL payment attempt={actualState} checkout={checkoutState} order={orderPaymentState}/{orderDocumentState} payment_job={paymentJobState} scenarios={scenarioCount} attempts={attemptCount} collections={collectionCount}");
    }

    public static async Task GrantReconciliationReadAsync(string configPath, string artifactPath)
    {
        var (_, admin) = Load(configPath);
        var artifact = JsonSerializer.Deserialize<SessionArtifact>(File.ReadAllText(artifactPath), ArtifactJson)
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
IF NOT EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@user AND Status='Active')
 THROW 51000,'SYNTHETIC_OWNER_NOT_FOUND',1;
IF EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@user AND Permission='reconciliation.read' AND ScopeKind='Dataset' AND ScopeId='demo_m0' AND RevokedAt IS NULL)
 THROW 51000,'RECONCILIATION_GRANT_ALREADY_EXISTS',1;
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
VALUES(@grant,@user,'Owner','reconciliation.read','Dataset','demo_m0',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(hour,2,SYSUTCDATETIME()),'issue15-fixture','manual reconciliation report acceptance');", connection);
        command.Parameters.AddWithValue("@grant", "issue15_grant_" + Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("@user", artifact.UserId);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS synthetic Issue15 Owner received only the reconciliation.read dataset grant");
    }

    public static async Task GrantReconciliationResolveAsync(string configPath, string artifactPath)
    {
        var (_, admin) = Load(configPath);
        var artifact = JsonSerializer.Deserialize<SessionArtifact>(File.ReadAllText(artifactPath), ArtifactJson)
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
IF NOT EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@user AND Status='Active')
 THROW 51000,'SYNTHETIC_OWNER_NOT_FOUND',1;
IF EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@user AND Permission='reconciliation.resolve' AND ScopeKind='Dataset' AND ScopeId='demo_m0' AND RevokedAt IS NULL)
 THROW 51000,'RECONCILIATION_RESOLVE_GRANT_ALREADY_EXISTS',1;
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
VALUES(@grant,@user,'Owner','reconciliation.resolve','Dataset','demo_m0',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(hour,2,SYSUTCDATETIME()),'issue15-fixture','notification browser acceptance');", connection);
        command.Parameters.AddWithValue("@grant", "issue15_resolve_grant_" + Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("@user", artifact.UserId);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS synthetic Issue15 Owner received a dataset-scoped reconciliation.resolve grant for notification browser acceptance");
    }

    public static async Task CorrectReconciliationSourceAsync(string configPath, string sourceId)
    {
        if (sourceId.Length is < 12 or > 100
            || !sourceId.StartsWith("collection_", StringComparison.Ordinal)
            || sourceId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
            throw new InvalidOperationException("ISSUE15_SOURCE_ID_INVALID");
        var (_, admin) = Load(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
UPDATE m0commerce.Collections SET Kind='Primary'
WHERE Dataset='demo_m0' AND Id=@id AND Kind<>'Primary';
IF @@ROWCOUNT <> 1 THROW 51000,'ISSUE15_SYNTHETIC_LATE_SOURCE_NOT_FOUND',1;", connection);
        command.Parameters.AddWithValue("@id", sourceId);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS Issue15 synthetic late source corrected on dedicated fixture");
    }

    public static async Task SeedReconciliationBrowserSourceAsync(string configPath, string artifactPath, string sourceId, string orderId)
    {
        ValidateReconciliationBrowserSourceIds(sourceId, orderId);
        var (_, admin) = Load(configPath);
        var artifact = JsonSerializer.Deserialize<SessionArtifact>(File.ReadAllText(artifactPath), ArtifactJson)
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
IF EXISTS(SELECT 1 FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@source)
 OR EXISTS(SELECT 1 FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order)
 THROW 51000,'ISSUE15_BROWSER_SOURCE_ALREADY_EXISTS',1;
INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body)
VALUES('demo_m0','Order',@order,@owner,1,@body);
INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected,Pending,Refunded,ShippingPaid,ShippingSlot)
VALUES('demo_m0',@source,@order,'Late',100,0,0,0,'Free');", connection);
        command.Parameters.AddWithValue("@source", sourceId);
        command.Parameters.AddWithValue("@order", orderId);
        command.Parameters.AddWithValue("@owner", artifact.UserId);
        command.Parameters.AddWithValue("@body", JsonSerializer.Serialize(new { id = orderId, orderState = "Confirmed", paymentState = "Succeeded", compensationRequired = false }));
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS Issue15 isolated reconciliation browser source seeded");
        Console.WriteLine("ISSUE15_BROWSER_SOURCE_READY:" + sourceId + ":" + orderId);
    }

    public static async Task CleanupReconciliationBrowserSourceAsync(string configPath, string artifactPath, string sourceId, string orderId)
    {
        ValidateReconciliationBrowserSourceIds(sourceId, orderId);
        var (_, admin) = Load(configPath);
        var caseId = "recon_" + M0Http.Hash(string.Join("\0", new[] { "NON_PRIMARY_COLLECTION_REQUIRES_REVIEW", sourceId, "", "" }))[..32];
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='ReconciliationNotification' AND JSON_VALUE(Body,'$.caseId')=@case;
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='ReconciliationCase' AND Id=@case;
DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@source AND OrderId=@order;
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order AND OwnerId=@owner;", connection);
        var artifact = JsonSerializer.Deserialize<SessionArtifact>(File.ReadAllText(artifactPath), ArtifactJson)
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        command.Parameters.AddWithValue("@case", caseId);
        command.Parameters.AddWithValue("@source", sourceId);
        command.Parameters.AddWithValue("@order", orderId);
        command.Parameters.AddWithValue("@owner", artifact.UserId);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS Issue15 isolated reconciliation browser source and case removed");
    }

    static void ValidateReconciliationBrowserSourceIds(string sourceId, string orderId)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(sourceId, @"\Acollection_issue15_browser_[0-9a-f]{32}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            || orderId != "order_issue15_browser_" + sourceId["collection_issue15_browser_".Length..])
            throw new InvalidOperationException("ISSUE15_BROWSER_SOURCE_IDS_INVALID");
    }

    public static async Task SetReconciliationReportQueryFailureAsync(string configPath, bool enabled)
    {
        var (_, admin) = Load(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(enabled ? """
IF DATABASE_PRINCIPAL_ID(N'm0_api') IS NULL OR OBJECT_ID(N'm0commerce.Collections',N'U') IS NULL
 THROW 51000,'ISSUE15_QUERY_FAULT_TARGET_INVALID',1;
IF EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0commerce.Collections') AND permission_name=N'SELECT' AND state=N'D')
 THROW 51000,'ISSUE15_QUERY_FAULT_ALREADY_PRESENT',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0commerce.Collections') AND permission_name=N'SELECT' AND state=N'G')
 THROW 51000,'ISSUE15_QUERY_GRANT_BASELINE_MISSING',1;
DENY SELECT ON OBJECT::m0commerce.Collections TO m0_api;
""" : """
IF DATABASE_PRINCIPAL_ID(N'm0_api') IS NULL OR OBJECT_ID(N'm0commerce.Collections',N'U') IS NULL
 THROW 51000,'ISSUE15_QUERY_FAULT_TARGET_INVALID',1;
IF EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0commerce.Collections') AND permission_name=N'SELECT' AND state=N'D')
 REVOKE SELECT ON OBJECT::m0commerce.Collections FROM m0_api;
IF NOT EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0commerce.Collections') AND permission_name=N'SELECT' AND state=N'G')
 GRANT SELECT ON OBJECT::m0commerce.Collections TO m0_api;
IF EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0commerce.Collections') AND permission_name=N'SELECT' AND state=N'D')
 OR NOT EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0commerce.Collections') AND permission_name=N'SELECT' AND state=N'G')
 THROW 51000,'ISSUE15_QUERY_GRANT_RESTORE_FAILED',1;
""", connection);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine(enabled
            ? "PASS Issue15 SQL query failure injected for m0_api on dedicated fixture"
            : "PASS Issue15 canonical m0_api SELECT grant restored on dedicated fixture");
    }

    public static async Task CleanupAsync(string configPath, string artifactPath)
    {
        var (_, admin) = Load(configPath);
        if (!File.Exists(artifactPath)) return;
        var artifact = JsonSerializer.Deserialize<SessionArtifact>(File.ReadAllText(artifactPath), ArtifactJson)
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        await using (var connection = new SqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            await using var command = new SqlCommand(@"
DECLARE @orders TABLE(Id varchar(100) PRIMARY KEY);
DECLARE @checkouts TABLE(Id varchar(100) PRIMARY KEY);
DECLARE @docs TABLE(Id varchar(100) PRIMARY KEY);
INSERT @orders SELECT Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND OwnerId IN(@browser,@user,@command);
INSERT @checkouts SELECT Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND OwnerId IN(@browser,@user,@command);
INSERT @docs SELECT Id FROM m0commerce.Documents d WHERE Dataset='demo_m0' AND
 (OwnerId IN(@browser,@user,@command) OR JSON_VALUE(Body,'$.orderId') IN(SELECT Id FROM @orders) OR JSON_VALUE(Body,'$.checkoutId') IN(SELECT Id FROM @checkouts));
DELETE FROM m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId IN(SELECT Id FROM @orders);
DELETE FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId IN(SELECT Id FROM @orders);
DELETE FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId IN(SELECT Id FROM @orders);
DELETE FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId IN(SELECT Id FROM @docs);
DELETE FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND (Actor IN(@user,@command) OR TargetId IN(SELECT Id FROM @docs));
DELETE FROM m0commerce.GuestCapabilities WHERE Dataset='demo_m0' AND (BrowserId=@browser OR OrderId IN(SELECT Id FROM @orders));
DELETE FROM m0commerce.PrivateDeliveries WHERE Dataset='demo_m0' AND RequestId IN(SELECT Id FROM m0commerce.GuestRequests WHERE Dataset='demo_m0' AND (BrowserId=@browser OR OrderId IN(SELECT Id FROM @orders)));
DELETE FROM m0commerce.GuestRequests WHERE Dataset='demo_m0' AND (BrowserId=@browser OR OrderId IN(SELECT Id FROM @orders));
DELETE FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor IN(@browser,@user,@command);
DELETE FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(SELECT Id FROM @docs);
DELETE m0.Challenges WHERE UserId=@user;
DELETE m0.Sessions WHERE UserId=@user;
DELETE m0.Grants WHERE UserId=@user;
DELETE m0.SecurityAudit WHERE UserId=@user;
DELETE m0.Users WHERE UserId=@user;
UPDATE m0.SecurityState SET Maintenance=@previousMaintenance WHERE Id=1;", connection, transaction);
            command.Parameters.AddWithValue("@browser", artifact.BrowserId);
            command.Parameters.AddWithValue("@user", artifact.UserId);
            command.Parameters.AddWithValue("@command", artifact.CommandActorId ?? "");
            command.Parameters.AddWithValue("@previousMaintenance", artifact.PreviousMaintenance);
            await command.ExecuteNonQueryAsync();
            var inventorySnapshots = artifact.CatalogInventory
                ?? [artifact.DemoSku with { Sku = "sku_demo_03" }];
            foreach (var snapshot in inventorySnapshots)
            {
                await using var restoreInventory = new SqlCommand(snapshot.Exists
                    ? "UPDATE m0commerce.Inventory SET Price=@price,Stock=@stock,Active=@active WHERE Dataset='demo_m0' AND Sku=@sku"
                    : "DELETE m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku", connection, transaction);
                restoreInventory.Parameters.AddWithValue("@sku", snapshot.Sku);
                if (snapshot.Exists)
                {
                    restoreInventory.Parameters.AddWithValue("@price", snapshot.Price);
                    restoreInventory.Parameters.AddWithValue("@stock", snapshot.Stock);
                    restoreInventory.Parameters.AddWithValue("@active", snapshot.Active);
                }
                await restoreInventory.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        File.Delete(artifactPath);
        Console.WriteLine("PASS synthetic Issue06 SQL rows and private session artifact removed; fixture preserved");
    }
}
