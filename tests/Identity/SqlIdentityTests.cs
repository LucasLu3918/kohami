using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Core;
using Kohami.M0.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Kohami.M0.Identity.Tests;

public sealed class IdentitySqlFactAttribute : FactAttribute
{
    public IdentitySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("M0_IDENTITY_SQL_TEST_CONFIG")))
            Skip = "NOT_EXECUTED: supply private, dedicated disposable SQL fixture config via M0_IDENTITY_SQL_TEST_CONFIG";
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
        public readonly M0Settings Settings;
        public readonly M0Crypto Crypto;
        public readonly M0Sql Sql;
        public readonly SecurityService Service;
        public readonly string User = M0Http.Id("itest");
        public readonly string Session = M0Http.Id("session");
        public readonly string Email;
        public readonly string EphemeralPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        readonly string migrator;
        bool previousMaintenance;

        public Fixture()
        {
            var path = Environment.GetEnvironmentVariable("M0_IDENTITY_SQL_TEST_CONFIG") ?? throw new InvalidOperationException("SQL_TEST_CONFIG_MISSING");
            var text = File.ReadAllText(path);
            var raw = JsonNode.Parse(text) as JsonObject ?? throw new InvalidOperationException("SQL_TEST_CONFIG_INVALID");
            if (raw["IdentitySqlTestDedicated"]?.GetValue<bool>() != true)
                throw new InvalidOperationException("DISPOSABLE_DEDICATED_FIXTURE_REQUIRED");
            Settings = JsonSerializer.Deserialize<M0Settings>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            migrator = raw["MigratorConnectionString"]?.GetValue<string>() ?? throw new InvalidOperationException("MIGRATOR_REQUIRED");
            Email = User + "@example.test";
            Sql = new(Settings);
            Crypto = M0Crypto.Load(Settings);
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
            if (builder.InitialCatalog != "KohamiM0") throw new InvalidOperationException("SQL_TEST_SCOPE_INVALID");
            builder["Server Certificate"] = Settings.SqlCertificatePath;
            var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task Initialize(bool withSession = true)
        {
            await using var connection = await Admin();
            await using var state = M0Sql.Command(connection, "SELECT Maintenance FROM m0.SecurityState WHERE Id=1");
            previousMaintenance = (bool)(await state.ExecuteScalarAsync())!;
            var now = DateTimeOffset.UtcNow;
            var hash = new PasswordHasher<string>().HashPassword(Email, EphemeralPassword);
            const string seed = "UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1; " +
                "INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status) VALUES(@User,@Email,@Hash,1,'Active'); " +
                "INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) " +
                "VALUES(@Grant,@User,'CustomerSupport','refund.request','Case','case_fixture',@Now,DATEADD(day,1,@Now),'fixture','isolated fixture');";
            var session = withSession ? " INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,AuthenticatedAt) VALUES(@Session,@User,1,'AdminAuthenticated',DATEADD(minute,5,@Now),DATEADD(minute,5,@Now),@Nonce,@Now);" : "";
            await using var command = M0Sql.Command(connection, seed + session, null,
                ("@User", User), ("@Email", Email), ("@Hash", hash), ("@Grant", M0Http.Id("grant")),
                ("@Session", Session), ("@Now", now), ("@Nonce", M0Http.Id("nonce")));
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
            await using var command = M0Sql.Command(connection,
                "DELETE m0.AuthFailures WHERE AccountKey=@AccountKey; DELETE m0.Challenges WHERE UserId=@User; " +
                "DELETE m0.Sessions WHERE UserId=@User; DELETE m0.Grants WHERE UserId=@User; " +
                "DELETE m0.SecurityAudit WHERE UserId=@User; DELETE m0.Users WHERE UserId=@User; " +
                "UPDATE m0.SecurityState SET Maintenance=@Maintenance WHERE Id=1",
                null, ("@AccountKey", M0Http.Hash(Email)), ("@User", User), ("@Maintenance", previousMaintenance));
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
        await using var query = M0Sql.Command(connection,
            "SELECT COUNT(*) FROM m0.Sessions WHERE UserId=@User AND Stage='AdminAuthenticated' AND RevokedAt IS NULL",
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

        var denied = await Assert.ThrowsAsync<M0Exception>(() => fixture.Service.CreateSession(context, CancellationToken.None));
        Assert.Equal(401, denied.Status);
        await using var connection = await fixture.Admin();
        await using var query = M0Sql.Command(connection,
            "SELECT COUNT(*) FROM m0.Sessions WHERE UserId=@User AND Stage='AdminAuthenticated' AND RevokedAt IS NULL",
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
        await using (var command = M0Sql.Command(connection,
            "UPDATE m0.Grants SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@User AND Permission='refund.request'",
            null, ("@User", fixture.User)))
            await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<M0Exception>(() => fixture.Service.RequirePermissionAsync(
            fixture.Context(fixture.Session), "refund.request", "Case", "case_fixture"));
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
        Assert.Contains("Max-Age=0", context.Response.Headers.SetCookie.ToString());
    }

    [IdentitySqlFact]
    public async Task ExpiryEqualityAndSecurityVersionChangeInvalidateTheSession()
    {
        await using var fixture = new Fixture();
        await fixture.Initialize();
        var now = DateTimeOffset.UtcNow;
        await using (var connection = await fixture.Admin())
        await using (var command = M0Sql.Command(connection,
            "UPDATE m0.Sessions SET ExpiresAt=@Now WHERE SessionId=@Session",
            null, ("@Session", fixture.Session), ("@Now", now)))
            await command.ExecuteNonQueryAsync();
        var service = new SecurityService(fixture.Sql, fixture.Crypto, new FixedClock(now),
            new UnconfiguredEmailEvidenceSink(), new ExactResourceScopeResolver());
        Assert.Null(await service.ResolveAsync(fixture.Context(fixture.Session)));

        await using (var connection = await fixture.Admin())
        await using (var command = M0Sql.Command(connection,
            "UPDATE m0.Sessions SET ExpiresAt=DATEADD(minute,5,@Now) WHERE SessionId=@Session; " +
            "UPDATE m0.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@User",
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
        await using var update = M0Sql.Command(connection,
            "UPDATE m0.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@User",
            null, ("@User", fixture.User));
        var denied = await Assert.ThrowsAsync<SqlException>(() => update.ExecuteNonQueryAsync());
        Assert.Equal(229, denied.Number);
    }
}
