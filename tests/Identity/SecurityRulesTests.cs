using Kohami.Security;
using Kohami.Catalog;
using Xunit;

namespace Kohami.Identity.Tests;

public sealed class SecurityRulesTests
{
    [Fact]
    public void ETagCannotBeBorrowedFromAnotherAggregateWithSameVersion()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers.IfMatch = SecurityRules.ETag("Grant", "grant_1", 1);
        var error = Assert.Throws<Kohami.Core.KohamiException>(() => SecurityRules.Match(context, "Grant", "grant_2", 1));
        Assert.Equal(412, error.Status);
    }

    [Fact]
    public void ForeignDatasetDoesNotMatch()
    {
        Assert.True(SecurityRules.ScopeMatches("Dataset", "kohami_local", "RefundCase", "case_1", "kohami_local"));
        Assert.False(SecurityRules.ScopeMatches("Dataset", "other", "RefundCase", "case_1", "kohami_local"));
    }

    [Fact]
    public void CaseScopeDoesNotCoverDifferentCase()
    {
        Assert.True(SecurityRules.ScopeMatches("Case", "case_1", "RefundCase", "case_1", "kohami_local"));
        Assert.False(SecurityRules.ScopeMatches("Case", "case_1", "RefundCase", "case_2", "kohami_local"));
    }

    [Fact]
    public void CoreOwnerAllowlistIsBounded()
    {
        Assert.Equal(24, SecurityRules.CorePermissions.Count);
        Assert.False(SecurityRules.IsCorePermission("asset.publish"));
        Assert.False(SecurityRules.IsCorePermission("fitting.manage"));
    }

    [Fact]
    public void PurposeIsBoundToKindAndPhaseTwoDenied()
    {
        Assert.Equal("RefundCase", SecurityRules.PurposeKind("refund.execute"));
        Assert.Throws<Kohami.Core.KohamiException>(() => SecurityRules.PurposeKind("fitting.manage"));
    }

    [Theory]
    [InlineData("123456789012345")]
    [InlineData("passwordpassword")]
    public void PasswordBlocklistIsApplied(string value) =>
        Assert.Throws<Kohami.Core.KohamiException>(() => SecurityRules.Password(value));

    [Fact]
    public void SupportRoleCannotAcquireRefundExecuteEvenIfDatabaseContainsBadGrant() =>
        Assert.False(SecurityRules.RoleAllows("CustomerSupport", "refund.execute", "Case"));

    [Fact]
    public void MerchandiserCannotAcquireDatasetWideStockScope() =>
        Assert.False(SecurityRules.RoleAllows("Merchandiser", "stock.adjust", "Dataset"));

    [Fact]
    public void MerchandiserCatalogReadIsDatasetScopedAndCannotWrite()
    {
        Assert.True(SecurityRules.IsCorePermission("catalog.read"));
        Assert.True(SecurityRules.RoleAllows("Merchandiser", "catalog.read", "Dataset"));
        Assert.False(SecurityRules.RoleAllows("Merchandiser", "catalog.read", "Product"));
        Assert.False(SecurityRules.RoleAllows("Merchandiser", "catalog.write", "Dataset"));
        Assert.False(SecurityRules.RoleAllows("Merchandiser", "access.manage", "Dataset"));
    }

    [Theory]
    [InlineData("demo@test.com", "demo@test.com")]
    [InlineData("Buyer+test@example.com", "buyer+test@example.com")]
    public void EmailAcceptsCommonInternetAddressesAndNormalizesCase(string input, string expected) =>
        Assert.Equal(expected, SecurityRules.Email(input));

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("buyer@localhost")]
    [InlineData("Buyer <buyer@example.com>")]
    [InlineData("buyer @example.com")]
    public void EmailRejectsMalformedAddresses(string input) =>
        Assert.Throws<Kohami.Core.KohamiException>(() => SecurityRules.Email(input));

    [Fact]
    public void AdminCatalogListAndDetailAreCatalogModuleReads()
    {
        Assert.True(CatalogEndpoints.UsesModuleContract("GET", "/api/v1/admin/products"));
        Assert.True(CatalogEndpoints.UsesModuleContract("GET", "/api/v1/admin/products/product_demo_01"));
        Assert.True(CatalogEndpoints.UsesModuleContract("POST", "/api/v1/admin/products"));
    }

    [Fact]
    public async Task MissingBusinessResourceBindingFailsClosed() =>
        await Assert.ThrowsAsync<Kohami.Core.KohamiException>(() => new ExactResourceScopeResolver().ResolveAsync("RefundCase", "unknown", default));
}
