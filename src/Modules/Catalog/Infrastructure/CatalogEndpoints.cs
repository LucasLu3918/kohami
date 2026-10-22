using System.Text.RegularExpressions;
using Kohami.Catalog.Application;
using Kohami.Core;
using Kohami.Catalog.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kohami.Catalog;

public static class CatalogEndpoints
{
    public static IServiceCollection AddFoundationCatalog(this IServiceCollection services)
    {
        services.AddSingleton<CatalogService>();
        services.AddSingleton<IPublishedSkuReader, SqlPublishedSkuReader>();
        return services;
    }

    public static bool UsesModuleContract(string method, string path) =>
        method == "GET" && (path == "/api/v1/products" || Regex.IsMatch(path, @"^/api/v1/products/[a-z0-9_-]+$") || path == "/api/v1/admin/products" || Regex.IsMatch(path, @"^/api/v1/admin/products/[^/]+$")) ||
        (method is "POST" or "PUT") && (path == "/api/v1/admin/products" || Regex.IsMatch(path, @"^/api/v1/admin/products/[^/]+(?:/publication-commands)?$"));

    public static RouteGroupBuilder MapFoundationCatalog(this RouteGroupBuilder group)
    {
        group.MapGet("/products", (HttpContext h, CatalogService s) => s.Products(h));
        group.MapGet("/products/{slug}", (HttpContext h, string slug, CatalogService s) => s.Product(h, slug));
        group.MapGet("/admin/products", (HttpContext h, CatalogService s) => s.AdminProducts(h));
        group.MapPost("/admin/products", (HttpContext h, CatalogService s) => s.Create(h));
        group.MapGet("/admin/products/{id}", (HttpContext h, string id, CatalogService s) => s.GetAdminProductOrDraft(h, id));
        group.MapPut("/admin/products/{id}", (HttpContext h, string id, CatalogService s) => s.Update(h, id));
        group.MapPost("/admin/products/{id}/publication-commands", (HttpContext h, string id, CatalogService s) => s.Publish(h, id));
        return group;
    }
}
