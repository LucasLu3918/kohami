using Kohami.Catalog.Application;
using Kohami.Core;

namespace Kohami.Catalog.Infrastructure;

internal sealed class SqlPublishedSkuReader(SqlRuntime sql) : IPublishedSkuReader
{
    public async Task<PublishedSkuVariant?> FindAsync(string skuId, CancellationToken cancellationToken = default)
    {
        await using var connection = await sql.OpenAsync(cancellationToken);
        await using var command = SqlRuntime.Command(connection,
            "SELECT TOP(1) ColorId,GarmentSize FROM identity_access.CatalogProductSkus WHERE Dataset='kohami_local' AND SkuId=@sku AND PublicIncluded=1 AND PublicActive=1",
            null, ("@sku", skuId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PublishedSkuVariant(reader.GetString(0), reader.GetString(1))
            : null;
    }
}
