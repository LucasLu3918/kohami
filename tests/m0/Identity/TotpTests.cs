using System.Text;
using Kohami.M0.Security;
using Xunit;
namespace Kohami.M0.Identity.Tests;

public sealed class TotpTests
{
    [Fact] public void ETagCannotBeBorrowedFromAnotherAggregateWithSameVersion()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers.IfMatch = SecurityRules.ETag("Grant", "grant_1", 1);
        var error = Assert.Throws<Kohami.M0.Core.M0Exception>(() => SecurityRules.Match(context, "Mfa", "user_1", 1));
        Assert.Equal(412, error.Status);
    }
    static byte[] Vector => Encoding.ASCII.GetBytes("12345678901234567890"); // Published RFC 6238 test vector, not a credential.
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Rfc6238PublicVectors(long seconds, string expected) => Assert.Equal(expected, Totp.Code(Vector, seconds / 30));
    [Fact] public void MatcherReturnsServerStepAndRejectsReplay() { Assert.Equal(1L, Totp.Match(Vector, "287082", DateTimeOffset.FromUnixTimeSeconds(59), -1)); Assert.Null(Totp.Match(Vector, "287082", DateTimeOffset.FromUnixTimeSeconds(59), 1)); }
    [Fact] public void OlderStepIsNeverReacceptedWhenClockRollsBack() => Assert.Null(Totp.Match(Vector, "287082", DateTimeOffset.FromUnixTimeSeconds(59), 100));
    [Theory][InlineData("0287082")][InlineData("28708x")][InlineData("")] public void StrictSixDigits(string value) => Assert.Null(Totp.Match(Vector, value, DateTimeOffset.FromUnixTimeSeconds(59), -1));
    [Fact] public void OutsideWindowRejected() => Assert.Null(Totp.Match(Vector, "287082", DateTimeOffset.FromUnixTimeSeconds(120), -1));
    [Fact] public void AdjacentWindowReturnsActualMatchedStep() => Assert.Equal(1L, Totp.Match(Vector, "287082", DateTimeOffset.FromUnixTimeSeconds(60), -1));
    [Fact] public void Base32RoundTrip() => Assert.Equal(Vector, Totp.Decode(Totp.Encode(Vector)));
    [Fact] public void ForeignDatasetDoesNotMatch() { Assert.True(SecurityRules.ScopeMatches("Dataset", "demo_m0", "RefundCase", "case_1", "demo_m0")); Assert.False(SecurityRules.ScopeMatches("Dataset", "other", "RefundCase", "case_1", "demo_m0")); }
    [Fact] public void CaseScopeDoesNotCoverDifferentCase() { Assert.True(SecurityRules.ScopeMatches("Case", "case_1", "RefundCase", "case_1", "demo_m0")); Assert.False(SecurityRules.ScopeMatches("Case", "case_1", "RefundCase", "case_2", "demo_m0")); }
    [Fact] public void CoreOwnerAllowlistIsBounded() { Assert.Equal(23, SecurityRules.CorePermissions.Count); Assert.False(SecurityRules.IsCorePermission("asset.publish")); Assert.False(SecurityRules.IsCorePermission("fitting.manage")); }
    [Fact] public void PurposeIsBoundToKindAndPhaseTwoDenied() { Assert.Equal("RefundCase", SecurityRules.PurposeKind("refund.execute")); Assert.Throws<Kohami.M0.Core.M0Exception>(() => SecurityRules.PurposeKind("fitting.manage")); }
    [Theory][InlineData("123456789012345")][InlineData("passwordpassword")] public void PasswordBlocklistIsApplied(string value) => Assert.Throws<Kohami.M0.Core.M0Exception>(() => SecurityRules.Password(value));
    [Fact] public void SupportRoleCannotAcquireRefundExecuteEvenIfDatabaseContainsBadGrant() => Assert.False(SecurityRules.RoleAllows("CustomerSupport", "refund.execute", "Case"));
    [Fact] public void MerchandiserCannotAcquireDatasetWideStockScope() => Assert.False(SecurityRules.RoleAllows("Merchandiser", "stock.adjust", "Dataset"));
    [Fact] public async Task MissingBusinessResourceBindingFailsClosed() => await Assert.ThrowsAsync<Kohami.M0.Core.M0Exception>(() => new ExactResourceScopeResolver().ResolveAsync("RefundCase", "unknown", default));
}
