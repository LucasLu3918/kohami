using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Kohami.M0.Security;

public sealed record ResourceScope(string Kind, string Id);
public interface IM0ResourceScopeResolver { Task<IReadOnlyList<ResourceScope>> ResolveAsync(string kind, string id, CancellationToken ct); }
public sealed class ExactResourceScopeResolver : IM0ResourceScopeResolver
{
    public Task<IReadOnlyList<ResourceScope>> ResolveAsync(string kind, string id, CancellationToken ct) =>
        kind is "User" or "Dataset" or "Credential" ? Task.FromResult<IReadOnlyList<ResourceScope>>([]) : throw new M0Exception(503, "SOURCE_UNBOUND");
}
public sealed class SecurityService(M0Sql sql, M0Crypto crypto, TimeProvider clock, IM0EmailEvidenceSink emailSink, IM0ResourceScopeResolver resources) : IM0Security, ICatalogOwnerCreationAuthorization
{
    public const string SessionCookie = "kohami.session", BrowserCookie = "kohami.account-browser";
    readonly PasswordHasher<string> hasher = new();
    readonly string dummyHash = new PasswordHasher<string>().HashPassword("dummy", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    DateTimeOffset Now => clock.GetUtcNow();
    static string S(JsonObject o, string n) => o[n]?.GetValue<string>() ?? "";
    static long N(JsonObject o, string n) => o[n]?.GetValue<long>() ?? 0;
    static bool B(JsonObject o, string n) => o[n]?.GetValue<bool>() ?? false;
    static DateTimeOffset D(JsonObject o, string n) => DateTimeOffset.Parse(S(o, n), CultureInfo.InvariantCulture);
    static JsonArray A(JsonObject o, string n) => o[n] as JsonArray ?? [];
    static void Shape(JsonObject body, params string[] fields) { if (body.Count != fields.Length || fields.Any(x => !body.ContainsKey(x)) || body.Any(x => !fields.Contains(x.Key, StringComparer.Ordinal) || x.Value is not JsonValue)) throw new M0Exception(422, "VALIDATION_ERROR"); }
    static string Text(JsonObject body, string field) { try { return body.Text(field); } catch (InvalidOperationException) { throw new M0Exception(422, "VALIDATION_ERROR"); } }
    sealed class Work(SqlConnection connection, SqlTransaction transaction) : IAsyncDisposable
    {
        public SqlConnection Connection => connection; public SqlTransaction Transaction => transaction;
        public async Task<JsonObject?> Read(string proc, CancellationToken ct, params (string, object?)[] values) { await using var cmd = M0Sql.Command(connection, proc, transaction, values); cmd.CommandType = CommandType.StoredProcedure; var value = await cmd.ExecuteScalarAsync(ct); if (value is not string raw || string.IsNullOrEmpty(raw)) return null; return JsonNode.Parse(raw) as JsonObject; }
        public async Task<object?> Run(string proc, CancellationToken ct, params (string, object?)[] values) { await using var cmd = M0Sql.Command(connection, proc, transaction, values); cmd.CommandType = CommandType.StoredProcedure; return await cmd.ExecuteScalarAsync(ct); }
        public Task Commit(CancellationToken ct) => transaction.CommitAsync(ct);
        public async ValueTask DisposeAsync() { await transaction.DisposeAsync(); await connection.DisposeAsync(); }
    }
    async Task<Work> Start(CancellationToken ct, bool worker = false) { var c = await sql.OpenAsync(ct, worker); try { var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, ct); var w = new Work(c, tx); await w.Run("m0.security_lock", ct); return w; } catch { await c.DisposeAsync(); throw; } }
    string? SessionId(HttpContext ctx) { string? value = null; foreach (var name in new[] { SessionCookie }) { var found = M0Http.Cookie(ctx, crypto, name); if (found is null) continue; if (value is not null) throw new M0Exception(401, "AUTHENTICATION_REQUIRED"); value = found; } if (value is null) return null; var parts = value.Split('|'); if (parts.Length != 2 || parts[0] != sql.Settings.DatasetId) return null; SecurityRules.Identifier(parts[1]); return parts[1]; }
    async Task<JsonObject?> Load(Work w, HttpContext ctx, CancellationToken ct) { var id = SessionId(ctx); return id is null ? null : await w.Read("m0.security_load_session", ct, ("@SessionId", id), ("@Now", Now)); }
    static string[] Permissions(JsonObject user, DateTimeOffset now) { if (S(user, "Stage") != "AdminAuthenticated" || S(user, "Status") != "Active" || B(user, "Maintenance")) return []; return A(user, "Grants").OfType<JsonObject>().Where(g => SecurityRules.RoleAllows(S(g, "Role"), S(g, "Permission"), S(g, "ScopeKind")) && (S(g, "ScopeKind") != "Dataset" || S(g, "ScopeId") == "demo_m0") && D(g, "ValidFrom") <= now && D(g, "ValidUntil") > now).Select(g => S(g, "Permission")).Distinct().ToArray(); }
    M0Principal Principal(JsonObject u) => new(S(u, "UserId"), S(u, "SessionId"), N(u, "SecurityVersion"), S(u, "Stage"), Permissions(u, Now));
    public async Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) { await using var w = await Start(ct); var u = await Load(w, context, ct); if (u is not null) await w.Run("m0.security_touch_session", ct, ("@SessionId", S(u, "SessionId")), ("@Now", Now)); await w.Commit(ct); return u is null ? null : Principal(u); }
    async Task Authorize(JsonObject u, string permission, string kind, string id, CancellationToken ct)
    {
        if (!SecurityRules.IsCorePermission(permission) || S(u, "Stage") != "AdminAuthenticated" || S(u, "Status") != "Active" || B(u, "Maintenance")) throw new M0Exception(403, "PERMISSION_CHANGED");
        var targets = new List<ResourceScope> { new(kind, id) }; targets.AddRange(await resources.ResolveAsync(kind, id, ct));
        var ok = A(u, "Grants").OfType<JsonObject>().Any(g => S(g, "Permission") == permission && D(g, "ValidFrom") <= Now && D(g, "ValidUntil") > Now && SecurityRules.RoleAllows(S(g, "Role"), S(g, "Permission"), S(g, "ScopeKind")) && targets.Any(t => SecurityRules.ScopeMatches(S(g, "ScopeKind"), S(g, "ScopeId"), t.Kind, t.Id, sql.Settings.DatasetId)));
        if (!ok) throw new M0Exception(403, "PERMISSION_CHANGED");
    }
    public async Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) { await using var w = await Start(ct); var u = await Load(w, context, ct) ?? throw new M0Exception(401, "AUTHENTICATION_REQUIRED"); await Authorize(u, permission, resourceKind, resourceId, ct); await w.Commit(ct); return Principal(u); }
    public async Task<M0Principal> RequireCatalogOwnerCreateAsync(HttpContext context, CancellationToken ct = default)
    {
        await using var w = await Start(ct);
        var u = await Load(w, context, ct) ?? throw new M0Exception(401, "AUTHENTICATION_REQUIRED");
        var stage = S(u, "Stage");
        var ownerGrant = A(u, "Grants").OfType<JsonObject>().Any(g => S(g, "Role") == "Owner" &&
            S(g, "Permission") == "catalog.write" && S(g, "ScopeKind") == "Dataset" &&
            S(g, "ScopeId") == sql.Settings.DatasetId && g["RevokedAt"] is null && D(g, "ValidFrom") <= Now && D(g, "ValidUntil") > Now);
        if (!ownerGrant || stage != "AdminAuthenticated" ||
            S(u, "Status") != "Active" || B(u, "Maintenance")) throw new M0Exception(403, "PERMISSION_CHANGED");
        await w.Commit(ct);
        return Principal(u);
    }
    public async Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) { if (!SecurityRules.IsCorePermission(permission)) return false; await using var w = await Start(ct, true); var result = await w.Run("m0.security_dispatch_allowed", ct, ("@UserId", actorUserId), ("@SecurityVersion", securityVersion), ("@Permission", permission), ("@ResourceKind", resourceKind), ("@ResourceId", resourceId), ("@DatasetId", sql.Settings.DatasetId), ("@Now", Now)); await w.Commit(ct); return result is true; }
    void Clear(HttpContext ctx) { foreach (var name in new[] { SessionCookie }) M0Http.DeleteCookie(ctx, name); }
    void Issue(HttpContext ctx, JsonObject u) { Clear(ctx); M0Http.Cookie(ctx, crypto, SessionCookie, sql.Settings.DatasetId + "|" + S(u, "SessionId"), D(u, "AbsoluteUntil")); }
    object SessionModel(JsonObject? u) => u is null ? new { state = "Anonymous", mode = "mock", userId = (string?)null, permissions = Array.Empty<string>(), expiresAt = (string?)null } : (object)new { state = S(u, "Stage") switch { "AdminAuthenticated" => "AdminAuthenticated", "MemberAuthenticated" => "MemberAuthenticated", _ => "Anonymous" }, mode = "mock", userId = S(u, "UserId"), permissions = Permissions(u, Now), expiresAt = S(u, "ExpiresAt") };
    async Task<JsonObject> Body(HttpContext ctx, CancellationToken ct, params string[] fields)
    {
        var body = await M0Http.BodyAsync(ctx, ct);
        Shape(body, fields);
        foreach (var field in fields)
        {
            var value = Text(body, field);
            if (field.EndsWith("Id", StringComparison.Ordinal) || field == "expectedVersion") SecurityRules.Identifier(value);
            if (field == "requestNonce") SecurityRules.Nonce(value);
            }
        return body;
    }
    public async Task<IResult> CreateSession(HttpContext ctx, CancellationToken ct)
    {
        var body = await Body(ctx, ct, "email", "password"); var email = SecurityRules.Email(Text(body, "email")); var password = Text(body, "password"); if (password.Length is < 1 or > 256) throw new M0Exception(422, "VALIDATION_ERROR");
        var now = Now; var account = M0Http.Hash(email); var browser = M0Http.Hash((ctx.Connection.RemoteIpAddress?.ToString() ?? "loopback") + "|" + (M0Http.Cookie(ctx, crypto, BrowserCookie) ?? "anonymous"));
        await using var w = await Start(ct); await w.Run("m0.security_throttle", ct, ("@AccountKey", account), ("@BrowserKey", browser), ("@IpKey", M0Http.Hash(ctx.Connection.RemoteIpAddress?.ToString() ?? "loopback")), ("@Now", now)); var user = await w.Read("m0.security_load_email", ct, ("@Email", email));
        var match = hasher.VerifyHashedPassword(email, user is null ? dummyHash : S(user, "PasswordHash"), password);
        if (user is null || match == PasswordVerificationResult.Failed || S(user, "Status") is "Suspended" or "Closed") { await w.Run("m0.security_throttle", ct, ("@AccountKey", account), ("@BrowserKey", browser), ("@IpKey", M0Http.Hash(ctx.Connection.RemoteIpAddress?.ToString() ?? "loopback")), ("@Now", now), ("@Failed", true)); await w.Commit(ct); throw new M0Exception(401, "AUTHENTICATION_REQUIRED"); }
        var id = M0Http.Id("session"); var nonce = M0Http.Id("nonce"); await w.Run("m0.security_create_session", ct, ("@UserId", S(user, "UserId")), ("@SecurityVersion", N(user, "SecurityVersion")), ("@SessionId", id), ("@Nonce", nonce), ("@Now", now));
        var u = await w.Read("m0.security_load_session", ct, ("@SessionId", id), ("@Now", now)) ?? throw new M0Exception(401, "AUTHENTICATION_REQUIRED"); await w.Commit(ct); Issue(ctx, u); return M0Http.Json(ctx, SessionModel(u));
    }
    public async Task<IResult> GetSession(HttpContext ctx, CancellationToken ct) { await using var w = await Start(ct); var u = await Load(w, ctx, ct); if (u is not null) { await w.Run("m0.security_touch_session", ct, ("@SessionId", S(u, "SessionId")), ("@Now", Now)); u = await Load(w, ctx, ct); } await w.Commit(ct); return M0Http.Json(ctx, SessionModel(u)); }
    public async Task<IResult> DeleteSession(HttpContext ctx, CancellationToken ct) { await using var w = await Start(ct); var id = SessionId(ctx); if (id is not null) await w.Run("m0.security_logout", ct, ("@SessionId", id), ("@Now", Now)); await w.Commit(ct); Clear(ctx); return Results.NoContent(); }
    (string Id, string Nonce, DateTimeOffset Expires) Browser(HttpContext ctx, bool create = false) { var value = M0Http.Cookie(ctx, crypto, BrowserCookie); if (value is not null) { var parts = value.Split('|'); if (parts.Length == 4 && parts[0] == sql.Settings.DatasetId && DateTimeOffset.TryParse(parts[3], CultureInfo.InvariantCulture, out var expiry) && expiry > Now) return (parts[1], parts[2], expiry); } if (!create) throw new M0Exception(401, "AUTHENTICATION_REQUIRED"); var id = M0Http.Id("browser"); var nonce = M0Http.Id("nonce"); var until = Now.AddMinutes(5); M0Http.Cookie(ctx, crypto, BrowserCookie, $"{sql.Settings.DatasetId}|{id}|{nonce}|{until:O}", until); return (id, nonce, until); }
    public Task<IResult> AccountContext(HttpContext ctx, CancellationToken ct) { var b = Browser(ctx, true); return Task.FromResult(M0Http.Json(ctx, new { requestNonce = b.Nonce, expiresAt = b.Expires, mode = "mock" })); }
    public async Task<IResult> RequestEmail(HttpContext ctx, CancellationToken ct) { var body = await Body(ctx, ct, "email", "requestNonce"); var email = SecurityRules.Email(Text(body, "email")); var b = Browser(ctx); if (Text(body, "requestNonce") != b.Nonce) throw new M0Exception(422, "VALIDATION_ERROR"); await using var w = await Start(ct); var user = await w.Read("m0.security_load_email", ct, ("@Email", email)); await w.Run("m0.security_throttle", ct, ("@AccountKey", M0Http.Hash("email:" + email)), ("@BrowserKey", M0Http.Hash("email:" + b.Id)), ("@IpKey", M0Http.Hash(ctx.Connection.RemoteIpAddress?.ToString() ?? "loopback")), ("@Now", Now), ("@Failed", true)); if (user is not null && !B(user, "EmailConfirmed") && S(user, "Status") is "SetupOnly" or "Active") { var id = M0Http.Id("email"); var proof = M0Http.Id("proof"); await w.Run("m0.security_create_challenge", ct, ("@ChallengeId", id), ("@UserId", S(user, "UserId")), ("@SessionId", b.Id), ("@Intent", "email"), ("@Nonce", b.Nonce), ("@Now", Now), ("@ExpiresAt", b.Expires), ("@ProofHash", M0Http.Hash(proof))); await emailSink.WriteAsync(email, id, proof, b.Nonce, b.Expires, ct); } await w.Commit(ct); return M0Http.Json(ctx, new { accepted = true, messageCode = "REQUEST_ACCEPTED" }, 202); }
    public async Task<IResult> ConfirmEmail(HttpContext ctx, CancellationToken ct) { var body = await Body(ctx, ct, "challengeId", "proof", "requestNonce"); var b = Browser(ctx); if (Text(body, "requestNonce") != b.Nonce) throw new M0Exception(422, "ACCOUNT_PROOF_INVALID"); await using var w = await Start(ct); var result = await w.Run("m0.security_confirm_email", ct, ("@ChallengeId", Text(body, "challengeId")), ("@SessionId", b.Id), ("@Nonce", b.Nonce), ("@ProofHash", M0Http.Hash(Text(body, "proof"))), ("@Now", Now)); await w.Commit(ct); if (result is not int n || n != 1) throw new M0Exception(422, "ACCOUNT_PROOF_INVALID"); return Results.NoContent(); }
    object GrantModel(JsonObject g) => new { id = S(g, "GrantId"), targetUserId = S(g, "UserId"), role = S(g, "Role"), permission = S(g, "Permission"), scope = new { kind = S(g, "ScopeKind"), id = S(g, "ScopeId") }, validFrom = S(g, "ValidFrom"), validUntil = S(g, "ValidUntil"), reason = S(g, "Reason"), grantedBy = S(g, "GrantedBy"), revokedAt = g["RevokedAt"]?.GetValue<string>(), version = SecurityRules.Version(N(g, "Version")), etag = SecurityRules.ETag("Grant", S(g, "GrantId"), N(g, "Version")) };
    public async Task<IResult> GetGrant(HttpContext ctx, string id, CancellationToken ct) { SecurityRules.Identifier(id); await using var w = await Start(ct); var u = await Load(w, ctx, ct) ?? throw new M0Exception(401, "AUTHENTICATION_REQUIRED"); var grant = await w.Read("m0.security_get_grant", ct, ("@GrantId", id)) ?? throw new M0Exception(404, "RESOURCE_NOT_FOUND"); await Authorize(u, "access.read", "User", S(grant, "UserId"), ct); if (!SecurityRules.IsCorePermission(S(grant, "Permission"))) throw new M0Exception(404, "RESOURCE_NOT_FOUND"); await w.Commit(ct); ctx.Response.Headers.ETag = SecurityRules.ETag("Grant", id, N(grant, "Version")); return M0Http.Json(ctx, GrantModel(grant)); }
    public async Task<IResult> RevokeGrant(HttpContext ctx, string id, CancellationToken ct)
    {
        SecurityRules.Identifier(id);
        var key = M0Http.Key(ctx);
        var reason = ctx.Request.Headers["X-Change-Reason"].ToString();
        if (reason.Length is < 1 or > 500 || reason.Contains('\r') || reason.Contains('\n')) throw new M0Exception(422, "VALIDATION_ERROR");
        var fingerprint = M0Http.Hash(JsonSerializer.Serialize(new { id, reason, ifMatch = ctx.Request.Headers.IfMatch.ToString(), dataset = sql.Settings.DatasetId }));
        await using var w = await Start(ct);
        var u = await Load(w, ctx, ct) ?? throw new M0Exception(401, "AUTHENTICATION_REQUIRED");
        var grant = await w.Read("m0.security_get_grant", ct, ("@GrantId", id)) ?? throw new M0Exception(404, "RESOURCE_NOT_FOUND");
        await Authorize(u, "access.manage", "User", S(grant, "UserId"), ct);
        if (!SecurityRules.IsCorePermission(S(grant, "Permission"))) throw new M0Exception(404, "RESOURCE_NOT_FOUND");
        var replay = await w.Read("m0.security_get_replay", ct, ("@Actor", S(u, "UserId")), ("@Key", key));
        if (replay is not null)
        {
            if (S(replay, "Fingerprint") != fingerprint) throw new M0Exception(409, "KEY_CONFLICT");
            await w.Commit(ct);
            return Results.NoContent();
        }
        SecurityRules.Match(ctx, "Grant", id, N(grant, "Version"));
        await w.Run("m0.security_revoke_grant", ct, ("@GrantId", id), ("@Actor", S(u, "UserId")), ("@ActorSessionId", S(u,"SessionId")), ("@ActorSecurityVersion", N(u,"SecurityVersion")), ("@Version", N(grant, "Version")), ("@Reason", reason), ("@Now", Now));
        await w.Run("m0.security_record_replay", ct, ("@Actor", S(u, "UserId")), ("@Key", key), ("@Fingerprint", fingerprint), ("@Now", Now));
        await w.Commit(ct);
        return Results.NoContent();
    }
}
