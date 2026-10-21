using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.Core;
using Kohami.Security;
using Kohami.Catalog.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Kohami.Identity.Tests;

public sealed class IdentitySqlFactAttribute : FactAttribute
{
    public IdentitySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Foundation_IDENTITY_SQL_TEST_CONFIG")))
            Skip = "NOT_EXECUTED: supply private, dedicated disposable SQL fixture config via Foundation_IDENTITY_SQL_TEST_CONFIG";
    }
}

[CollectionDefinition("Identity SQL", DisableParallelization = true)]
public sealed class IdentitySqlCollection { }

[Collection("Identity SQL")]
public sealed class SqlIdentityTests
{
    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    sealed class Fixture : IAsyncDisposable
    {
        public readonly RuntimeSettings Settings;
        public readonly SessionCrypto Crypto;
        public readonly SqlRuntime Sql;
        public readonly SecurityService Service;
        public readonly string User = FoundationHttp.Id("itest");
        public readonly string Session = FoundationHttp.Id("session");
        public readonly string Email;
        public readonly string EphemeralPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        readonly string migrator;
        bool previousMaintenance;

        public Fixture()
        {
            var path = Environment.GetEnvironmentVariable("Foundation_IDENTITY_SQL_TEST_CONFIG") ?? throw new InvalidOperationException("SQL_TEST_CONFIG_MISSING");
            var text = File.ReadAllText(path);
            var raw = JsonNode.Parse(text) as JsonObject ?? throw new InvalidOperationException("SQL_TEST_CONFIG_INVALID");
            if (raw["IdentitySqlTestDedicated"]?.GetValue<bool>() != true)
                throw new InvalidOperationException("DISPOSABLE_DEDICATED_FIXTURE_REQUIRED");
            Settings = JsonSerializer.Deserialize<RuntimeSettings>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            migrator = raw["MigratorConnectionString"]?.GetValue<string>() ?? throw new InvalidOperationException("MIGRATOR_REQUIRED");
            Email = User + "@example.test";
            Sql = new(Settings);
            Crypto = SessionCrypto.Load(Settings);
            Service = new(Sql, Crypto, TimeProvider.System, new UnconfiguredEmailEvidenceSink(), new ExactResourceScopeResolver());
        }

        public async Task<SqlConnection> Admin()
        {
            Sql.ValidateTls();
            var builder = new SqlConnectionStringBuilder(migrator)
            {
                Encrypt = SqlConnectionEncryptOption.Strict,
                TrustServerCertificate = false,
                HostNameInCertificate = Settings.SqlServerName
            };
            if (builder.InitialCatalog != "Kohami") throw new InvalidOperationException("SQL_TEST_SCOPE_INVALID");
            builder["Server Certificate"] = Settings.SqlCertificatePath;
            var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task Initialize(bool withSession = true, string role = "CustomerSupport",
            string permission = "refund.request", string scopeKind = "Case", string scopeId = "case_fixture")
        {
            await using var connection = await Admin();
            await using var state = SqlRuntime.Command(connection, "SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1");
            previousMaintenance = (bool)(await state.ExecuteScalarAsync())!;
            var now = DateTimeOffset.UtcNow;
            var hash = new PasswordHasher<string>().HashPassword(Email, EphemeralPassword);
            const string seed = "UPDATE identity_access.SecurityState SET Maintenance=0 WHERE Id=1; " +
                "INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,Status) VALUES(@User,@Email,@Hash,1,'Active'); " +
                "INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) " +
                "VALUES(@Grant,@User,@Role,@Permission,@ScopeKind,@ScopeId,@Now,DATEADD(day,1,@Now),'fixture','isolated fixture');";
            var session = withSession ? " INSERT identity_access.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,AuthenticatedAt) VALUES(@Session,@User,1,'AdminAuthenticated',DATEADD(minute,5,@Now),DATEADD(minute,5,@Now),@Nonce,@Now);" : "";
            await using var command = SqlRuntime.Command(connection, seed + session, null,
                ("@User", User), ("@Email", Email), ("@Hash", hash), ("@Grant", FoundationHttp.Id("grant")),
                ("@Role", role), ("@Permission", permission), ("@ScopeKind", scopeKind), ("@ScopeId", scopeId),
                ("@Session", Session), ("@Now", now), ("@Nonce", FoundationHttp.Id("nonce")));
            await command.ExecuteNonQueryAsync();
        }

        public HttpContext Context(string id)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.Cookie = SecurityService.SessionCookie + "=" + Crypto.Sessions
                .CreateProtector(SecurityService.SessionCookie)
                .Protect(Settings.DatasetId + "|" + id);
            return context;
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = await Admin();
            await using var command = SqlRuntime.Command(connection,
                "DELETE identity_access.AuthFailures WHERE AccountKey=@AccountKey; DELETE identity_access.Challenges WHERE UserId=@User; " +
                "DELETE identity_access.Sessions WHERE UserId=@User; DELETE identity_access.Grants WHERE UserId=@User; " +
                "DELETE identity_access.SecurityAudit WHERE UserId=@User; DELETE identity_access.Users WHERE UserId=@User; " +
                "UPDATE identity_access.SecurityState SET Maintenance=@Maintenance WHERE Id=1",
                null, ("@AccountKey", FoundationHttp.Hash(Email)), ("@User", User), ("@Maintenance", previousMaintenance));
            await command.ExecuteNonQueryAsync();
        }
    }

    [IdentitySqlFact]
    public async Task PasswordLoginCreatesAnAuthenticatedSessionFromTheDatabaseAccount()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize(withSession: false);
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JsonObject
        {
            ["email"] = fixture.Email,
            ["password"] = fixture.EphemeralPassword
        }.ToJsonString()));

        var result = await fixture.Service.CreateSession(context, CancellationToken.None);
        var response = JsonSerializer.SerializeToNode(Assert.IsAssignableFrom<IValueHttpResult>(result).Value)!.AsObject();
        Assert.Equal("AdminAuthenticated", response["state"]!.GetValue<string>());
        Assert.Equal(fixture.User, response["userId"]!.GetValue<string>());
        await using var connection = await fixture.Admin();
        await using var query = SqlRuntime.Command(connection,
            "SELECT COUNT(*) FROM identity_access.Sessions WHERE UserId=@User AND Stage='AdminAuthenticated' AND RevokedAt IS NULL",
            null, ("@User", fixture.User));
        Assert.Equal(1, (int)(await query.ExecuteScalarAsync())!);
    }

    [IdentitySqlFact]
    public async Task WrongPasswordDoesNotCreateAnAuthenticatedSession()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize(withSession: false);
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JsonObject
        {
            ["email"] = fixture.Email,
            ["password"] = "not-the-account-password"
        }.ToJsonString()));

        var denied = await Assert.ThrowsAsync<KohamiException>(() => fixture.Service.CreateSession(context, CancellationToken.None));
        Assert.Equal(401, denied.Status);
        await using var connection = await fixture.Admin();
        await using var query = SqlRuntime.Command(connection,
            "SELECT COUNT(*) FROM identity_access.Sessions WHERE UserId=@User AND Stage='AdminAuthenticated' AND RevokedAt IS NULL",
            null, ("@User", fixture.User));
        Assert.Equal(0, (int)(await query.ExecuteScalarAsync())!);
    }

    [IdentitySqlFact]
    public async Task SessionRequiresTheSameDatasetAndCurrentGrant()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize();
        var principal = await fixture.Service.ResolveAsync(fixture.Context(fixture.Session));
        Assert.NotNull(principal);
        Assert.Equal("AdminAuthenticated", principal.Stage);
        Assert.Contains("refund.request", principal.Permissions);

        var foreign = new DefaultHttpContext();
        foreign.Request.Headers.Cookie = SecurityService.SessionCookie + "=" + fixture.Crypto.Sessions
            .CreateProtector(SecurityService.SessionCookie).Protect("another_dataset|" + fixture.Session);
        Assert.Null(await fixture.Service.ResolveAsync(foreign));

        await using (var connection = await fixture.Admin())
        await using (var command = SqlRuntime.Command(connection,
            "UPDATE identity_access.Grants SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@User AND Permission='refund.request'",
            null, ("@User", fixture.User)))
            await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<KohamiException>(() => fixture.Service.RequirePermissionAsync(
            fixture.Context(fixture.Session), "refund.request", "Case", "case_fixture"));
    }

    [IdentitySqlFact]
    public async Task CatalogReadRequiresTheDatasetGrantAndCannotAuthorizeWrites()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize(role: "Merchandiser", permission: "catalog.read", scopeKind: "Dataset", scopeId: "kohami_local");
        var principal = await fixture.Service.RequirePermissionAsync(
            fixture.Context(fixture.Session), "catalog.read", "Dataset", "kohami_local");
        Assert.Contains("catalog.read", principal.Permissions);

        await Assert.ThrowsAsync<KohamiException>(() => fixture.Service.RequirePermissionAsync(
            fixture.Context(fixture.Session), "catalog.write", "Product", "product_demo_01"));

        await using var connection = await fixture.Admin();
        await using var revoke = SqlRuntime.Command(connection,
            "UPDATE identity_access.Grants SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@User AND Permission='catalog.read'",
            null, ("@User", fixture.User));
        await revoke.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<KohamiException>(() => fixture.Service.RequirePermissionAsync(
            fixture.Context(fixture.Session), "catalog.read", "Dataset", "kohami_local"));
    }

    [IdentitySqlFact]
    public async Task CatalogReadRejectsAProductScopedOrForeignDatasetGrant()
    {
        await using (var productScoped = new Fixture())
        {
            await productScoped.Initialize(role: "Merchandiser", permission: "catalog.read",
                scopeKind: "Product", scopeId: "product_demo_01");
            var denied = await Assert.ThrowsAsync<KohamiException>(() => productScoped.Service.RequirePermissionAsync(
                productScoped.Context(productScoped.Session), "catalog.read", "Dataset", "kohami_local"));
            Assert.Equal(403, denied.Status);
        }

        await using (var foreignDataset = new Fixture())
        {
            await foreignDataset.Initialize(role: "Merchandiser", permission: "catalog.read",
                scopeKind: "Dataset", scopeId: "another_dataset");
            var denied = await Assert.ThrowsAsync<KohamiException>(() => foreignDataset.Service.RequirePermissionAsync(
                foreignDataset.Context(foreignDataset.Session), "catalog.read", "Dataset", "kohami_local"));
            Assert.Equal(403, denied.Status);
        }
    }

    [IdentitySqlFact]
    public async Task AdminCatalogListAndDetailReturnPublishedProjectionAndRejectWrites()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize(role: "Merchandiser", permission: "catalog.read", scopeKind: "Dataset", scopeId: "kohami_local");
        var catalog = new CatalogService(fixture.Sql, fixture.Service, fixture.Service, TimeProvider.System);
        var listResult = await catalog.AdminProducts(fixture.Context(fixture.Session));
        var list = JsonSerializer.SerializeToNode(Assert.IsAssignableFrom<IValueHttpResult>(listResult).Value)!.AsObject();
        var items = list["items"]!.AsArray();
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.NotNull(item!["skus"]));
        Assert.All(items, item => Assert.Null(item!["draftJson"]));

        var productId = items[0]!["id"]!.GetValue<string>();
        var detail = await catalog.GetAdminProductOrDraft(fixture.Context(fixture.Session), productId);
        var product = JsonSerializer.SerializeToNode(Assert.IsAssignableFrom<IValueHttpResult>(detail).Value)!.AsObject();
        Assert.Equal(productId, product["id"]!.GetValue<string>());
        Assert.NotNull(product["translations"]);
        Assert.Null(product["draftJson"]);

        var deniedWrite = await Assert.ThrowsAsync<KohamiException>(() => catalog.Create(fixture.Context(fixture.Session)));
        Assert.Equal(403, deniedWrite.Status);
    }

    [IdentitySqlFact]
    public async Task LogoutRevokesSessionAndClearsItsCookie()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize();
        var context = fixture.Context(fixture.Session);
        var result = await fixture.Service.DeleteSession(context, CancellationToken.None);
        Assert.Equal(StatusCodes.Status204NoContent, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(await fixture.Service.ResolveAsync(fixture.Context(fixture.Session)));
        var clearedCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("kohami.session=;", clearedCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", clearedCookie, StringComparison.OrdinalIgnoreCase);
    }

    [IdentitySqlFact]
    public async Task ExpiryEqualityAndSecurityVersionChangeInvalidateTheSession()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize();
        var now = DateTimeOffset.UtcNow;
        await using (var connection = await fixture.Admin())
        await using (var command = SqlRuntime.Command(connection,
            "UPDATE identity_access.Sessions SET ExpiresAt=@Now WHERE SessionId=@Session",
            null, ("@Session", fixture.Session), ("@Now", now)))
            await command.ExecuteNonQueryAsync();
        var service = new SecurityService(fixture.Sql, fixture.Crypto, new FixedClock(now),
            new UnconfiguredEmailEvidenceSink(), new ExactResourceScopeResolver());
        Assert.Null(await service.ResolveAsync(fixture.Context(fixture.Session)));

        await using (var connection = await fixture.Admin())
        await using (var command = SqlRuntime.Command(connection,
            "UPDATE identity_access.Sessions SET ExpiresAt=DATEADD(minute,5,@Now) WHERE SessionId=@Session; " +
            "UPDATE identity_access.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@User",
            null, ("@Session", fixture.Session), ("@User", fixture.User), ("@Now", now)))
            await command.ExecuteNonQueryAsync();
        Assert.Null(await service.ResolveAsync(fixture.Context(fixture.Session)));
    }

    [IdentitySqlFact]
    public async Task RuntimeCannotWriteUsersOrExecuteNativeProvisioning()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize();
        await using var connection = await fixture.Sql.OpenAsync();
        await using var update = SqlRuntime.Command(connection,
            "UPDATE identity_access.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@User",
            null, ("@User", fixture.User));
        var denied = await Assert.ThrowsAsync<SqlException>(() => update.ExecuteNonQueryAsync());
        Assert.Equal(229, denied.Number);
    }
}
