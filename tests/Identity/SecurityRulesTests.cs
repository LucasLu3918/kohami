using Kohami.M0.Security;
using Kohami.M0.Catalog;
using Xunit;

namespace Kohami.M0.Identity.Tests;

public sealed class SecurityRulesTests
{
    [Fact]
    public void ETagCannotBeBorrowedFromAnotherAggregateWithSameVersion()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers.IfMatch = SecurityRules.ETag("Grant", "grant_1", 1);
        var error = Assert.Throws<Kohami.M0.Core.M0Exception>(() => SecurityRules.Match(context, "Grant", "grant_2", 1));
        Assert.Equal(412, error.Status);
    }

    [Fact]
    public void ForeignDatasetDoesNotMatch()
    {
        Assert.True(SecurityRules.ScopeMatches("Dataset", "demo_m0", "RefundCase", "case_1", "demo_m0"));
        Assert.False(SecurityRules.ScopeMatches("Dataset", "other", "RefundCase", "case_1", "demo_m0"));
    }

    [Fact]
    public void CaseScopeDoesNotCoverDifferentCase()
    {
        Assert.True(SecurityRules.ScopeMatches("Case", "case_1", "RefundCase", "case_1", "demo_m0"));
        Assert.False(SecurityRules.ScopeMatches("Case", "case_1", "RefundCase", "case_2", "demo_m0"));
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
        Assert.Throws<Kohami.M0.Core.M0Exception>(() => SecurityRules.PurposeKind("fitting.manage"));
    }

    [Theory]
    [InlineData("123456789012345")]
    [InlineData("passwordpassword")]
    public void PasswordBlocklistIsApplied(string value) =>
        Assert.Throws<Kohami.M0.Core.M0Exception>(() => SecurityRules.Password(value));

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

    [Fact]
    public void AdminCatalogListAndDetailAreCatalogModuleReads()
    {
        Assert.True(CatalogEndpoints.UsesModuleContract("GET", "/api/v1/admin/products"));
        Assert.True(CatalogEndpoints.UsesModuleContract("GET", "/api/v1/admin/products/product_demo_01"));
        Assert.True(CatalogEndpoints.UsesModuleContract("POST", "/api/v1/admin/products"));
    }

    [Fact]
    public async Task MissingBusinessResourceBindingFailsClosed() =>
        await Assert.ThrowsAsync<Kohami.M0.Core.M0Exception>(() => new ExactResourceScopeResolver().ResolveAsync("RefundCase", "unknown", default));
}
