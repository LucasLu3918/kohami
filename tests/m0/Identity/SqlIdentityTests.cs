using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kohami.M0.Core;
using Kohami.M0.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Xunit;
namespace Kohami.M0.Identity.Tests;

public sealed class IdentitySqlFactAttribute : FactAttribute { public IdentitySqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("M0_IDENTITY_SQL_TEST_CONFIG"))) Skip = "NOT_EXECUTED: supply private, dedicated disposable SQL fixture config via M0_IDENTITY_SQL_TEST_CONFIG"; } }
[CollectionDefinition("Identity SQL", DisableParallelization = true)] public sealed class IdentitySqlCollection { }
[Collection("Identity SQL")]
public sealed class SqlIdentityTests
{
    sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    sealed class Fixture : IAsyncDisposable
    {
        public readonly M0Settings Settings; public readonly M0Crypto Crypto; public readonly M0Sql Sql; public readonly SecurityService Service;
        public readonly string User = M0Http.Id("itest"), Session = M0Http.Id("session"), Nonce = M0Http.Id("nonce"), Credential = M0Http.Id("credential"); public readonly byte[] Seed = RandomNumberGenerator.GetBytes(20);
        readonly string migrator; bool previousMaintenance;
        public Fixture() { var path = Environment.GetEnvironmentVariable("M0_IDENTITY_SQL_TEST_CONFIG") ?? throw new InvalidOperationException("SQL_TEST_CONFIG_MISSING"); var text = File.ReadAllText(path); var raw = JsonNode.Parse(text) as JsonObject ?? throw new InvalidOperationException("SQL_TEST_CONFIG_INVALID"); if (raw["IdentitySqlTestDedicated"]?.GetValue<bool>() != true) throw new InvalidOperationException("DISPOSABLE_DEDICATED_FIXTURE_REQUIRED"); Settings = JsonSerializer.Deserialize<M0Settings>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; migrator = raw["MigratorConnectionString"]?.GetValue<string>() ?? throw new InvalidOperationException("MIGRATOR_REQUIRED"); Sql = new(Settings); Crypto = M0Crypto.Load(Settings); Service = new(Sql, Crypto, TimeProvider.System, new UnconfiguredEmailEvidenceSink(), new ExactResourceScopeResolver()); }
        public async Task<SqlConnection> Admin() { Sql.ValidateTls(); var b = new SqlConnectionStringBuilder(migrator) { Encrypt = SqlConnectionEncryptOption.Strict, TrustServerCertificate = false, HostNameInCertificate = Settings.SqlServerName }; if (b.InitialCatalog != "KohamiM0") throw new InvalidOperationException("SQL_TEST_SCOPE_INVALID"); b["Server Certificate"] = Settings.SqlCertificatePath; var c = new SqlConnection(b.ConnectionString); await c.OpenAsync(); return c; }
        public async Task Initialize(bool withCredential = true)
        {
            await using var c = await Admin(); await using var state = M0Sql.Command(c, "SELECT Maintenance FROM m0.SecurityState WHERE Id=1"); previousMaintenance = (bool)(await state.ExecuteScalarAsync())!;
            var now = DateTimeOffset.UtcNow; var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var hash = new PasswordHasher<string>().HashPassword(User, password); var encrypted = Crypto.Totp.CreateProtector("kohami.m0.totp-seed", Settings.DatasetId, User).Protect(Totp.Encode(Seed));
            await using var cmd = M0Sql.Command(c, "UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1; INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,EncryptedSeed,CredentialId) VALUES(@User,@Email,@Hash,1,'Active',@Seed,@Credential); INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) VALUES(@Grant,@User,'CustomerSupport','refund.request','Case','case_fixture',@Now,DATEADD(day,1,@Now),'fixture','isolated fixture'); INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce) VALUES(@Session,@User,1,'AdminPasswordVerified',DATEADD(minute,5,@Now),DATEADD(minute,5,@Now),@Nonce)", null, ("@User", User), ("@Email", User + "@example.test"), ("@Hash", hash), ("@Seed", withCredential ? encrypted : null), ("@Credential", withCredential ? Credential : null), ("@Grant", M0Http.Id("grant")), ("@Session", Session), ("@Now", now), ("@Nonce", Nonce)); await cmd.ExecuteNonQueryAsync();
        }
        public HttpContext Context(string id, string cookie = SecurityService.StageCookie, JsonObject? body = null) { var c = new DefaultHttpContext(); c.Request.Headers.Cookie = cookie + "=" + Crypto.Sessions.CreateProtector(cookie).Protect(Settings.DatasetId + "|" + id); if (body is not null) c.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body.ToJsonString())); return c; }
        public async Task<string> Challenge(string intent, string nonce) { var id = M0Http.Id("challenge"); await using var c = await Sql.OpenAsync(); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(); await Proc(c, tx, "m0.security_lock"); await Proc(c, tx, "m0.security_create_challenge", ("@ChallengeId", id), ("@UserId", User), ("@SessionId", Session), ("@Intent", intent), ("@Nonce", nonce), ("@Now", DateTimeOffset.UtcNow), ("@ExpiresAt", DateTimeOffset.UtcNow.AddMinutes(4))); await tx.CommitAsync(); return id; }
        public static async Task<object?> Proc(SqlConnection c, SqlTransaction? tx, string name, params (string, object?)[] values) { await using var cmd = M0Sql.Command(c, name, tx, values); cmd.CommandType = CommandType.StoredProcedure; return await cmd.ExecuteScalarAsync(); }
        public async ValueTask DisposeAsync() { await using var c = await Admin(); await using var cmd = M0Sql.Command(c, "DELETE m0.RecentProofs WHERE UserId=@User; DELETE m0.Challenges WHERE UserId=@User; DELETE m0.Sessions WHERE UserId=@User; DELETE m0.RecoveryCodes WHERE UserId=@User; DELETE m0.Grants WHERE UserId=@User; DELETE m0.SecurityAudit WHERE UserId=@User; DELETE m0.Users WHERE UserId=@User; UPDATE m0.SecurityState SET Maintenance=@Maintenance WHERE Id=1", null, ("@User", User), ("@Maintenance", previousMaintenance)); await cmd.ExecuteNonQueryAsync(); }
    }
    [IdentitySqlFact] public async Task PasswordStageCannotEnterBusinessAndClientFlagsAreRejected() { await using var f = new Fixture(); await f.Initialize(); var principal = await f.Service.ResolveAsync(f.Context(f.Session)); Assert.NotNull(principal); Assert.Empty(principal.Permissions); Assert.Equal("AdminPasswordVerified", principal.Stage); var ex = await Assert.ThrowsAsync<M0Exception>(() => f.Service.RequirePermissionAsync(f.Context(f.Session), "refund.execute", "RefundCase", "case_fixture")); Assert.Equal(403, ex.Status); var c = await f.Challenge("login", f.Nonce); var body = new JsonObject { ["challengeId"] = c, ["code"] = Totp.Code(f.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30), ["requestNonce"] = f.Nonce, ["permissions"] = new JsonArray("refund.execute") }; await Assert.ThrowsAsync<M0Exception>(() => f.Service.VerifyLogin(f.Context(f.Session, body: body), default)); }
    [IdentitySqlFact]
    public async Task SameTotpStepAcrossTwoConcurrentLoginProofsOnlyOneCommit()
    {
        await using var f = new Fixture(); await f.Initialize(); var challenge = await f.Challenge("login", f.Nonce); var code = Totp.Code(f.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30); async Task<bool> Attempt() { try { await f.Service.VerifyLogin(f.Context(f.Session, body: new JsonObject { ["challengeId"] = challenge, ["code"] = code, ["requestNonce"] = f.Nonce }), default); return true; } catch (M0Exception ex) when (ex.Status is 401 or 422) { return false; } }
        var results = await Task.WhenAll(Attempt(), Attempt()); Assert.Single(results, x => x); await using var c = await f.Admin(); await using var cmd = M0Sql.Command(c, "SELECT COUNT(*) FROM m0.Sessions WHERE UserId=@User AND Stage='AdminMfaVerified' AND RevokedAt IS NULL", null, ("@User", f.User)); Assert.Equal(1, (int)(await cmd.ExecuteScalarAsync())!);
    }
    [IdentitySqlFact] public async Task RecoveryCodeConcurrentConsumptionOnlyRebindsAndCannotRestoreGrant() { await using var f = new Fixture(); await f.Initialize(); var code = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)); await using (var c = await f.Admin()) { await using var cmd = M0Sql.Command(c, "INSERT m0.RecoveryCodes(UserId,SetVersion,CodeHash) VALUES(@User,1,@Hash)", null, ("@User", f.User), ("@Hash", M0Http.Hash(code))); await cmd.ExecuteNonQueryAsync(); } var challenge = await f.Challenge("recovery", f.Nonce); async Task<bool> Attempt() { try { await f.Service.ConfirmRecovery(f.Context(f.Session, body: new JsonObject { ["challengeId"] = challenge, ["recoveryCode"] = code, ["requestNonce"] = f.Nonce }), default); return true; } catch (M0Exception ex) when (ex.Status is 401 or 422) { return false; } } var results = await Task.WhenAll(Attempt(), Attempt()); Assert.Single(results, x => x); await using var connection = await f.Admin(); await using var state = M0Sql.Command(connection, "SELECT Status FROM m0.Users WHERE UserId=@User", null, ("@User", f.User)); Assert.Equal("RebindRequired", (string)(await state.ExecuteScalarAsync())!); await using var session = M0Sql.Command(connection, "SELECT COUNT(*) FROM m0.Sessions WHERE UserId=@User AND Stage='AdminMfaVerified' AND RevokedAt IS NULL", null, ("@User", f.User)); Assert.Equal(0, (int)(await session.ExecuteScalarAsync())!); }
    [IdentitySqlFact] public async Task RuntimeCannotWriteUsersOrExecuteNativeBootstrap() { await using var f = new Fixture(); await f.Initialize(); await using var c = await f.Sql.OpenAsync(); await using var direct = M0Sql.Command(c, "UPDATE m0.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@User", null, ("@User", f.User)); var denied = await Assert.ThrowsAsync<SqlException>(() => direct.ExecuteNonQueryAsync()); Assert.Equal(229, denied.Number); var native = await Assert.ThrowsAsync<SqlException>(() => Fixture.Proc(c, null, "m0.security_native_prepare_owner", ("@UserId", M0Http.Id("itest")), ("@Email", "other@example.test"), ("@PasswordHash", "fixture-not-valid-passwordhash"), ("@OperationId", Guid.NewGuid()))); Assert.Equal(229, native.Number); }
    [IdentitySqlFact]
    public async Task ExpiryEqualityAndCurrentSecurityVersionImmediatelyInvalidateSession()
    {
        await using var f = new Fixture(); await f.Initialize();
        var now = DateTimeOffset.UtcNow;
        await using (var c = await f.Admin())
        {
            await using var cmd = M0Sql.Command(c, "UPDATE m0.Sessions SET ExpiresAt=@Now WHERE SessionId=@Session", null, ("@Session", f.Session), ("@Now", now));
            await cmd.ExecuteNonQueryAsync();
        }
        var service = new SecurityService(f.Sql, f.Crypto, new FixedClock(now), new UnconfiguredEmailEvidenceSink(), new ExactResourceScopeResolver());
        Assert.Null(await service.ResolveAsync(f.Context(f.Session))); // exactly equal, not merely elapsed
        await using (var c = await f.Admin())
        {
            await using var cmd = M0Sql.Command(c, "UPDATE m0.Sessions SET ExpiresAt=DATEADD(minute,5,@Now) WHERE SessionId=@Session; UPDATE m0.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@User", null, ("@Session", f.Session), ("@User", f.User), ("@Now", now));
            await cmd.ExecuteNonQueryAsync();
        }
        Assert.Null(await service.ResolveAsync(f.Context(f.Session))); // current authority invalidates a time-valid cookie
    }
    [IdentitySqlFact]
    public async Task EnrollmentSecretsIssueOnceAndConfirmationDoesNotCreateManagementSession()
    {
        await using var f = new Fixture(); await f.Initialize(withCredential: false);
        var request = f.Context(f.Session, body: new JsonObject { ["requestNonce"] = f.Nonce, ["expectedVersion"] = "v1" });
        request.Request.Headers.IfMatch = SecurityRules.ETag("Mfa", f.User, 1);
        var materialResult = await f.Service.Enroll(request, default);
        var material = JsonSerializer.SerializeToNode(Assert.IsAssignableFrom<IValueHttpResult>(materialResult).Value)!.AsObject();
        var enrollment = material["enrollmentId"]!.GetValue<string>();
        var key = Totp.Decode(material["sharedKey"]!.GetValue<string>());
        var code = Totp.Code(key, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var proof = new JsonObject { ["enrollmentId"] = enrollment, ["code"] = code, ["requestNonce"] = f.Nonce };
        var result = await f.Service.ConfirmEnrollment(f.Context(f.Session, body: proof), default);
        var confirmed = JsonSerializer.SerializeToNode(Assert.IsAssignableFrom<IValueHttpResult>(result).Value)!.AsObject();
        Assert.Equal("ReauthenticateRequired", confirmed["state"]!.GetValue<string>());
        Assert.Equal(10, confirmed["recoveryCodes"]!.AsArray().Count);
        Assert.True(confirmed["recoveryCodes"]!.AsArray().All(value => value!.GetValue<string>().Length == 32));
        Assert.Null(await f.Service.ResolveAsync(f.Context(f.Session)));
        await Assert.ThrowsAsync<M0Exception>(() => f.Service.ConfirmEnrollment(f.Context(f.Session, body: proof.DeepClone().AsObject()), default));
    }
}
