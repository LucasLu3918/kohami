using System.Security.Cryptography;
using System.Text.Json;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.WebUtilities;

internal static class SyntheticOwnerAcceptance
{
    sealed record InventorySnapshot(bool Exists, long Price, int Stock, bool Active);
    sealed record SessionArtifact(string UserId, string BrowserId, string SessionId, string SessionCookie, string BrowserCookie, bool PreviousMaintenance, InventorySnapshot DemoSku, string? GuestReadCookie = null, string? GuestActionCookie = null);
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
        if (resourceId is not null)
        {
            await AddRefundProofAsync(configPath, artifactPath, resourceId);
            return;
        }
        if (File.Exists(artifactPath) || Directory.Exists(artifactPath))
            throw new InvalidOperationException("SYNTHETIC_ARTIFACT_ALREADY_EXISTS");

        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var user = "issue06_owner_" + suffix;
        var browser = "issue06_browser_" + suffix;
        var session = "issue06_session_" + suffix;
        var nonce = Guid.NewGuid().ToString("N");
        var expires = now.AddHours(1);
        var proof = Guid.NewGuid().ToString("N");
        bool previousMaintenance;
        InventorySnapshot demoSku;
        await using (var inspect = new SqlConnection(admin.ConnectionString))
        {
            await inspect.OpenAsync();
            await using (var command = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", inspect))
                previousMaintenance = (bool)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("FIXTURE_STATE_INVALID"));
            await using var inventory = new SqlCommand("SELECT Price,Stock,Active FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03'", inspect);
            await using var reader = await inventory.ExecuteReaderAsync();
            demoSku = await reader.ReadAsync()
                ? new InventorySnapshot(true, reader.GetInt64(0), reader.GetInt32(1), reader.GetBoolean(2))
                : new InventorySnapshot(false, 650, 0, false);
        }

        var crypto = M0Crypto.Load(settings);
        var sessionCookie = WebEncoders.Base64UrlEncode(crypto.Sessions.CreateProtector("kohami.session").Protect(System.Text.Encoding.UTF8.GetBytes("demo_m0|" + session)));
        var browserPayload = JsonSerializer.Serialize(new { value = browser, until = expires });
        var browserCookie = WebEncoders.Base64UrlEncode(crypto.Sessions.CreateProtector("kohami.browser").Protect(System.Text.Encoding.UTF8.GetBytes(browserPayload)));
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        var artifact = new SessionArtifact(user, browser, session, sessionCookie, browserCookie, previousMaintenance, demoSku);
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
UPDATE m0.Users SET EncryptedSeed=N'issue06-synthetic-preauth-only-not-a-totp-seed' WHERE UserId=@user;
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
SELECT 'issue06_grant_'+CONVERT(nvarchar(36),NEWID()),@user,'Owner',p.Permission,'Dataset','demo_m0',DATEADD(minute,-1,@now),DATEADD(hour,2,@now),'issue06-fixture','synthetic API acceptance only'
FROM (VALUES('orders.read'),('demo.control'),('shipment.manage'),('support.respond'),('return.inspect'),('refund.execute')) p(Permission);
INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,MfaVerifiedAt)
VALUES(@session,@user,1,'AdminMfaVerified',DATEADD(hour,1,@now),DATEADD(hour,1,@now),@nonce,@now);
INSERT m0.RecentProofs(SessionId,UserId,SecurityVersion,Purpose,ResourceKind,ResourceId,RequestNonce,VerifiedAt,ValidUntil)
VALUES(@session,@user,1,'demo.control','Dataset','demo_m0',@proof,@now,DATEADD(minute,5,@now));", connection, transaction);
            command.Parameters.AddWithValue("@user", user);
            command.Parameters.AddWithValue("@email", user + "@example.test");
            command.Parameters.AddWithValue("@session", session);
            command.Parameters.AddWithValue("@nonce", nonce);
            command.Parameters.AddWithValue("@proof", proof);
            command.Parameters.AddWithValue("@previousMaintenance", previousMaintenance);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            File.Delete(artifactPath);
            throw;
        }
        Console.WriteLine("PASS synthetic Issue06 Owner session prepared; no login or TOTP was performed");
    }

    public static async Task AddRefundProofAsync(string configPath, string artifactPath, string refundCaseId)
    {
        var (_, admin) = Load(configPath);
        var artifact = JsonSerializer.Deserialize<SessionArtifact>(File.ReadAllText(artifactPath), ArtifactJson)
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        if (!System.Text.RegularExpressions.Regex.IsMatch(refundCaseId, "^refundcase_[A-Za-z0-9_-]{1,90}$"))
            throw new InvalidOperationException("REFUND_CASE_ID_INVALID");
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
DECLARE @now datetimeoffset=SYSUTCDATETIME();
IF NOT EXISTS(SELECT 1 FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundCase' AND Id=@caseId AND JSON_VALUE(Body,'$.state')='Requested') THROW 51000,'REFUND_CASE_NOT_FOUND',1;
INSERT m0.RecentProofs(SessionId,UserId,SecurityVersion,Purpose,ResourceKind,ResourceId,RequestNonce,VerifiedAt,ValidUntil)
VALUES(@session,@user,1,'refund.execute','RefundCase',@caseId,@nonce,@now,DATEADD(minute,5,@now));", connection);
        command.Parameters.AddWithValue("@caseId", refundCaseId);
        command.Parameters.AddWithValue("@session", artifact.SessionId);
        command.Parameters.AddWithValue("@user", artifact.UserId);
        command.Parameters.AddWithValue("@nonce", Guid.NewGuid().ToString("N"));
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS synthetic refund authorization proof prepared for the requested API case");
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
        await using (var connection = new SqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"
IF NOT EXISTS(SELECT 1 FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order AND OwnerId=@browser) THROW 51000,'SYNTHETIC_ORDER_NOT_FOUND',1;
INSERT m0commerce.GuestCapabilities(Dataset,Id,OrderId,BrowserId,Intent,ExpiresAt,Consumed) VALUES('demo_m0',@read,@order,@browser,'read',@expires,0),('demo_m0',@return,@order,@browser,'return',@expires,0);", connection);
            command.Parameters.AddWithValue("@order", orderId);
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
INSERT @orders SELECT Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND OwnerId=@browser;
INSERT @checkouts SELECT Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND OwnerId=@browser;
INSERT @docs SELECT Id FROM m0commerce.Documents d WHERE Dataset='demo_m0' AND
 (OwnerId IN(@browser,@user) OR JSON_VALUE(Body,'$.orderId') IN(SELECT Id FROM @orders) OR JSON_VALUE(Body,'$.checkoutId') IN(SELECT Id FROM @checkouts));
DELETE FROM m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId IN(SELECT Id FROM @orders);
DELETE FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId IN(SELECT Id FROM @orders);
DELETE FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId IN(SELECT Id FROM @orders);
DELETE FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId IN(SELECT Id FROM @docs);
DELETE FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND (Actor=@user OR TargetId IN(SELECT Id FROM @docs));
DELETE FROM m0commerce.GuestCapabilities WHERE Dataset='demo_m0' AND (BrowserId=@browser OR OrderId IN(SELECT Id FROM @orders));
DELETE FROM m0commerce.PrivateDeliveries WHERE Dataset='demo_m0' AND RequestId IN(SELECT Id FROM m0commerce.GuestRequests WHERE Dataset='demo_m0' AND (BrowserId=@browser OR OrderId IN(SELECT Id FROM @orders)));
DELETE FROM m0commerce.GuestRequests WHERE Dataset='demo_m0' AND (BrowserId=@browser OR OrderId IN(SELECT Id FROM @orders));
DELETE FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor IN(@browser,@user);
DELETE FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(SELECT Id FROM @docs);
DELETE m0.RecentProofs WHERE UserId=@user;
DELETE m0.Challenges WHERE UserId=@user;
DELETE m0.Sessions WHERE UserId=@user;
DELETE m0.Grants WHERE UserId=@user;
DELETE m0.SecurityAudit WHERE UserId=@user;
DELETE m0.Users WHERE UserId=@user;
IF @skuExists=1 UPDATE m0commerce.Inventory SET Price=@skuPrice,Stock=@skuStock,Active=@skuActive WHERE Dataset='demo_m0' AND Sku='sku_demo_03';
ELSE DELETE m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03';
UPDATE m0.SecurityState SET Maintenance=@previousMaintenance WHERE Id=1;", connection, transaction);
            command.Parameters.AddWithValue("@browser", artifact.BrowserId);
            command.Parameters.AddWithValue("@user", artifact.UserId);
            command.Parameters.AddWithValue("@previousMaintenance", artifact.PreviousMaintenance);
            command.Parameters.AddWithValue("@skuExists", artifact.DemoSku.Exists);
            command.Parameters.AddWithValue("@skuPrice", artifact.DemoSku.Price);
            command.Parameters.AddWithValue("@skuStock", artifact.DemoSku.Stock);
            command.Parameters.AddWithValue("@skuActive", artifact.DemoSku.Active);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        File.Delete(artifactPath);
        Console.WriteLine("PASS synthetic Issue06 SQL rows and private session artifact removed; fixture preserved");
    }
}
