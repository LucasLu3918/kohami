using Kohami.M0.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Kohami.M0.Security;

public static class SecurityEndpoints
{
    public static IServiceCollection AddM0Security(this IServiceCollection services) { services.TryAddSingleton(TimeProvider.System); services.TryAddSingleton<IM0EmailEvidenceSink, UnconfiguredEmailEvidenceSink>(); services.TryAddSingleton<IM0ResourceScopeResolver, ExactResourceScopeResolver>(); services.AddSingleton<SecurityService>(); services.AddSingleton<IM0Security>(s => s.GetRequiredService<SecurityService>()); return services; }
    public static RouteGroupBuilder MapM0Security(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/sessions", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.CreateSession(ctx, ct));
        api.MapGet("/auth/session", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.GetSession(ctx, ct));
        api.MapDelete("/auth/session", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.DeleteSession(ctx, ct));
        api.MapGet("/auth/account-context", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.AccountContext(ctx, ct));
        api.MapPost("/auth/email-verification-requests", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.RequestEmail(ctx, ct));
        api.MapPost("/auth/email-verifications", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.ConfirmEmail(ctx, ct));
        api.MapGet("/auth/mfa-context", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.MfaContext(ctx, ct));
        api.MapPost("/auth/mfa-enrollments", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.Enroll(ctx, ct));
        api.MapPost("/auth/mfa-enrollments/confirm", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.ConfirmEnrollment(ctx, ct));
        api.MapPost("/auth/mfa-verifications", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.VerifyLogin(ctx, ct));
        api.MapPost("/auth/reauthentication-challenges", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.ReauthChallenge(ctx, ct));
        api.MapPost("/auth/reauthentications", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.VerifyReauth(ctx, ct));
        api.MapPost("/auth/mfa-recoveries", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.RecoveryChallenge(ctx, ct));
        api.MapPost("/auth/mfa-recoveries/confirm", (HttpContext ctx, SecurityService s, CancellationToken ct) => s.ConfirmRecovery(ctx, ct));
        api.MapGet("/admin/access-grants/{id}", (HttpContext ctx, string id, SecurityService s, CancellationToken ct) => s.GetGrant(ctx, id, ct));
        api.MapDelete("/admin/access-grants/{id}", (HttpContext ctx, string id, SecurityService s, CancellationToken ct) => s.RevokeGrant(ctx, id, ct));
        return api;
    }
}
