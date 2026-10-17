using System.Text.Json;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;

namespace Kohami.Catalog.Tests;

internal static class CatalogSqlAcceptance
{
    private sealed record ProductEvidence(string UserId, string ProductId, string SkuId, string Slug, long FinalPriceTwd);
    private sealed record SaveFailureEvidence(string UserId, string Slug, string SkuId, string Key);

    private static SqlConnectionStringBuilder Admin(string configPath)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = config.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean()
            || root.GetProperty("datasetId").GetString() != "demo_m0")
            throw new InvalidOperationException("DEDICATED_ISSUE08_FIXTURE_REQUIRED");
        var settings = M0Settings.Load(configPath);
        var connection = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName,
        };
        connection["Server Certificate"] = settings.SqlCertificatePath;
        var portText = connection.DataSource.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase) ? connection.DataSource["localhost,".Length..] : "";
        if (!int.TryParse(portText, out var port) || port is < 14336 or > 14399
            || !string.Equals(connection.InitialCatalog, "KohamiM0", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ISSUE08_FIXTURE_TARGET_INVALID");
        return connection;
    }

    private static ProductEvidence Evidence(string path) =>
        JsonSerializer.Deserialize<ProductEvidence>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("ISSUE08_EVIDENCE_INVALID");

    public static async Task AddOwnerGrantAsync(string configPath, string artifactPath)
    {
        _ = Admin(configPath);
        using var artifact = JsonDocument.Parse(File.ReadAllText(artifactPath));
        var userId = artifact.RootElement.GetProperty("userId").GetString()
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        await using var connection = new SqlConnection(Admin(configPath).ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
VALUES(@grant,@user,'Owner','catalog.write','Dataset','demo_m0',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(hour,2,SYSUTCDATETIME()),'issue08-fixture','synthetic Issue08 acceptance only');
""", connection);
        command.Parameters.AddWithValue("@grant", "issue08_grant_" + Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("@user", userId);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS scoped synthetic Owner catalog.write grant prepared; no application login was performed");
    }

    public static async Task AddStockOwnerGrantAsync(string configPath, string artifactPath)
    {
        using var artifact = JsonDocument.Parse(File.ReadAllText(artifactPath));
        var userId = artifact.RootElement.GetProperty("userId").GetString()
            ?? throw new InvalidOperationException("SYNTHETIC_ARTIFACT_INVALID");
        await using var connection = new SqlConnection(Admin(configPath).ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
VALUES(@grant,@user,'Owner','stock.adjust','Dataset','demo_m0',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(hour,2,SYSUTCDATETIME()),'issue09-fixture','synthetic Issue09 acceptance only');
""", connection);
        command.Parameters.AddWithValue("@grant", "issue09_grant_" + Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("@user", userId);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS synthetic Owner stock.adjust grant prepared; no application login was performed");
    }

    public static async Task SetSaveFailureAsync(string configPath, bool enabled)
    {
        var admin = Admin(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(enabled ? """
IF OBJECT_ID(N'm0commerce.issue08_acceptance_fail_inventory_insert', N'TR') IS NOT NULL
 THROW 51000,'ISSUE08_FAULT_ALREADY_PRESENT',1;
EXEC(N'CREATE TRIGGER m0commerce.issue08_acceptance_fail_inventory_insert ON m0commerce.Inventory AFTER INSERT AS
BEGIN
 IF EXISTS(SELECT 1 FROM inserted WHERE Sku LIKE N''issue08-fail-%'')
  THROW 51008, ''ISSUE08_INJECTED_SAVE_FAILURE'', 1;
END');
""" : """
IF OBJECT_ID(N'm0commerce.issue08_acceptance_fail_inventory_insert', N'TR') IS NOT NULL
BEGIN
 IF OBJECT_DEFINITION(OBJECT_ID(N'm0commerce.issue08_acceptance_fail_inventory_insert')) NOT LIKE N'%ISSUE08_INJECTED_SAVE_FAILURE%'
  THROW 51000,'ISSUE08_FAULT_OWNERSHIP_MISMATCH',1;
 DROP TRIGGER m0commerce.issue08_acceptance_fail_inventory_insert;
END;
""", connection);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine(enabled ? "PASS Issue08 save failure injection enabled on dedicated fixture" : "PASS Issue08 save failure injection removed");
    }

    public static async Task SetReadFailureAsync(string configPath, bool enabled)
    {
        var admin = Admin(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(enabled ? """
IF DATABASE_PRINCIPAL_ID(N'm0_api') IS NULL OR OBJECT_ID(N'm0.CatalogProductSkus',N'U') IS NULL
 THROW 51000,'ISSUE08_READ_FAULT_TARGET_INVALID',1;
IF EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0.CatalogProductSkus') AND permission_name=N'SELECT' AND state=N'D')
 THROW 51000,'ISSUE08_READ_FAULT_ALREADY_PRESENT',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0.CatalogProductSkus') AND permission_name=N'SELECT' AND state=N'G')
 THROW 51000,'ISSUE08_READ_GRANT_BASELINE_MISSING',1;
DENY SELECT ON OBJECT::m0.CatalogProductSkus TO m0_api;
""" : """
IF EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=DATABASE_PRINCIPAL_ID(N'm0_api') AND major_id=OBJECT_ID(N'm0.CatalogProductSkus') AND permission_name=N'SELECT' AND state=N'D')
BEGIN
 REVOKE SELECT ON OBJECT::m0.CatalogProductSkus FROM m0_api;
 GRANT SELECT ON OBJECT::m0.CatalogProductSkus TO m0_api;
END;
""", connection);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine(enabled ? "PASS Issue08 public Catalog read failure injected for m0_api on dedicated fixture" : "PASS Issue08 Catalog read permission restored to canonical grant");
    }

    public static async Task VerifySaveFailureAsync(string configPath, string evidencePath)
    {
        _ = Admin(configPath);
        var evidence = JsonSerializer.Deserialize<SaveFailureEvidence>(File.ReadAllText(evidencePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("ISSUE08_FAILURE_EVIDENCE_INVALID");
        if (!System.Text.RegularExpressions.Regex.IsMatch(evidence.UserId, "^issue06_owner_[0-9a-f]{32}$")
            || !System.Text.RegularExpressions.Regex.IsMatch(evidence.Slug, "^issue08-fail-[0-9a-f]{32}$")
            || !System.Text.RegularExpressions.Regex.IsMatch(evidence.SkuId, "^issue08-fail-[0-9a-f]{32}-demo-black-m$")
            || !System.Text.RegularExpressions.Regex.IsMatch(evidence.Key, "^issue08-fail-[0-9a-f]{32}$"))
            throw new InvalidOperationException("ISSUE08_FAILURE_EVIDENCE_SCOPE_INVALID");
        await using var connection = new SqlConnection(Admin(configPath).ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
SELECT
 (SELECT COUNT(*) FROM m0.CatalogProducts WHERE Dataset='demo_m0' AND Slug=@slug),
 (SELECT COUNT(*) FROM m0.CatalogProductSkus WHERE Dataset='demo_m0' AND SkuId=@sku),
 (SELECT COUNT(*) FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku),
 (SELECT COUNT(*) FROM m0.CatalogAuditEvents WHERE Dataset='demo_m0' AND JSON_VALUE(Details,'$.slug')=@slug AND Action='ProductCreated'),
 (SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@user AND OperationKey=@key);
""", connection);
        command.Parameters.AddWithValue("@slug", evidence.Slug);
        command.Parameters.AddWithValue("@sku", evidence.SkuId);
        command.Parameters.AddWithValue("@user", evidence.UserId);
        command.Parameters.AddWithValue("@key", evidence.Key);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE08_SAVE_FAILURE_EVIDENCE_MISSING");
        for (var i = 0; i < 5; i++)
            if (reader.GetInt32(i) != 0) throw new InvalidOperationException("ISSUE08_SAVE_FAILURE_LEFT_PARTIAL_ROWS");
        Console.WriteLine("PASS failed Issue08 save left no Product, SKU, Inventory, create-audit, or idempotency rows");
    }

    public static async Task VerifyAndCleanupAsync(string configPath, string evidencePath)
    {
        var admin = Admin(configPath);
        var evidence = Evidence(evidencePath);
        ValidateEvidence(evidence);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        try
        {
            await using var command = new SqlCommand("""
SELECT p.State,p.PublicSlug,p.OwnerId,p.PublicJson,i.Price,i.Active,
       (SELECT COUNT(*) FROM m0.CatalogAuditEvents a WHERE a.Dataset=p.Dataset AND a.ProductId=p.ProductId AND a.ActorId=@user AND a.Action='ProductCreated'),
       (SELECT COUNT(*) FROM m0.CatalogAuditEvents a WHERE a.Dataset=p.Dataset AND a.ProductId=p.ProductId AND a.ActorId=@user AND a.Action='ProductDraftUpdated'),
       (SELECT COUNT(*) FROM m0.CatalogAuditEvents a WHERE a.Dataset=p.Dataset AND a.ProductId=p.ProductId AND a.ActorId=@user AND a.Action='ProductPublished'),
       (SELECT COUNT(*) FROM m0.CatalogAuditEvents a WHERE a.Dataset=p.Dataset AND a.ProductId=p.ProductId AND a.ActorId=@user AND a.Action='ProductUnpublished'),
       (SELECT COUNT(*) FROM m0.CatalogProducts x WHERE x.Dataset=p.Dataset AND x.Slug=@slug),
       (SELECT COUNT(*) FROM m0.CatalogProductSkus x WHERE x.Dataset=p.Dataset AND x.SkuId=@sku AND x.ProductId=p.ProductId)
FROM m0.CatalogProducts p
JOIN m0.CatalogProductSkus s ON s.Dataset=p.Dataset AND s.ProductId=p.ProductId AND s.SkuId=@sku
JOIN m0commerce.Inventory i ON i.Dataset=s.Dataset AND i.Sku=s.SkuId
WHERE p.Dataset='demo_m0' AND p.ProductId=@product AND p.Slug=@slug;
""", connection);
            command.Parameters.AddWithValue("@user", evidence.UserId);
            command.Parameters.AddWithValue("@slug", evidence.Slug);
            command.Parameters.AddWithValue("@sku", evidence.SkuId);
            command.Parameters.AddWithValue("@product", evidence.ProductId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE08_SQL_PRODUCT_MISSING");
            var state = reader.GetString(0);
            var publicSlug = reader.IsDBNull(1) ? null : reader.GetString(1);
            var ownerId = reader.GetString(2);
            var publicJson = reader.IsDBNull(3) ? null : reader.GetString(3);
            var price = reader.GetInt64(4);
            var active = reader.GetBoolean(5);
            var created = reader.GetInt32(6);
            var updated = reader.GetInt32(7);
            var published = reader.GetInt32(8);
            var unpublished = reader.GetInt32(9);
            var slugCount = reader.GetInt32(10);
            var skuCount = reader.GetInt32(11);
            await reader.CloseAsync();
            if (state != "Unpublished" || publicSlug is not null || ownerId != evidence.UserId
                || publicJson is null || publicJson.Contains("sourceRightsRef", StringComparison.Ordinal)
                || publicJson.Contains("synthetic_owned_demo", StringComparison.Ordinal)
                || price != evidence.FinalPriceTwd || active
                || created != 1 || updated != 1 || published != 2 || unpublished != 1
                || slugCount != 1 || skuCount != 1)
                throw new InvalidOperationException("ISSUE08_SQL_ACCEPTANCE_ASSERTION_FAILED");
            Console.WriteLine("PASS SQL persisted one synthetic Product/SKU, final Inventory price, Unpublished state, sanitized public snapshot, and create/update/publish/unpublish audit events");
        }
        finally
        {
            await CleanupAsync(connection, evidence);
        }
    }

    public static async Task CleanupFromEvidenceAsync(string configPath, string evidencePath)
    {
        if (!File.Exists(evidencePath)) return;
        var admin = Admin(configPath);
        var evidence = Evidence(evidencePath);
        ValidateEvidence(evidence);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await CleanupAsync(connection, evidence);
        Console.WriteLine("PASS Issue08 synthetic Catalog rows removed; fixture preserved");
    }

    private static void ValidateEvidence(ProductEvidence evidence)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(evidence.UserId, "^issue06_owner_[0-9a-f]{32}$")
            || !System.Text.RegularExpressions.Regex.IsMatch(evidence.ProductId, "^product_[0-9a-f]{24}$")
            || !System.Text.RegularExpressions.Regex.IsMatch(evidence.SkuId, "^issue08-[0-9a-f]{32}-demo-black-m$")
            || !System.Text.RegularExpressions.Regex.IsMatch(evidence.Slug, "^issue08-[0-9a-f]{32}$")
            || evidence.FinalPriceTwd != 1550)
            throw new InvalidOperationException("ISSUE08_EVIDENCE_SCOPE_INVALID");
    }

    private static async Task CleanupAsync(SqlConnection connection, ProductEvidence evidence)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = new SqlCommand("""
IF EXISTS(SELECT 1 FROM m0.CatalogProducts WHERE Dataset='demo_m0' AND ProductId=@product AND (OwnerId<>@user OR Slug<>@slug))
 THROW 51000,'ISSUE08_CLEANUP_SCOPE_MISMATCH',1;
DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@user AND OperationKey LIKE 'issue08-%';
DELETE m0.CatalogAuditEvents WHERE Dataset='demo_m0' AND ProductId=@product AND EXISTS(SELECT 1 FROM m0.CatalogProducts WHERE Dataset='demo_m0' AND ProductId=@product AND OwnerId=@user AND Slug=@slug);
DELETE m0.CatalogProductSkus WHERE Dataset='demo_m0' AND ProductId=@product AND EXISTS(SELECT 1 FROM m0.CatalogProducts WHERE Dataset='demo_m0' AND ProductId=@product AND OwnerId=@user AND Slug=@slug);
DELETE m0.CatalogProducts WHERE Dataset='demo_m0' AND ProductId=@product AND OwnerId=@user AND Slug=@slug;
DELETE m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku AND NOT EXISTS(SELECT 1 FROM m0.CatalogProductSkus WHERE Dataset='demo_m0' AND SkuId=@sku);
""", connection, transaction);
        command.Parameters.AddWithValue("@user", evidence.UserId);
        command.Parameters.AddWithValue("@product", evidence.ProductId);
        command.Parameters.AddWithValue("@sku", evidence.SkuId);
        command.Parameters.AddWithValue("@slug", evidence.Slug);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
}
