namespace Kohami.Catalog.Application;

public sealed record PublishedSkuVariant(string ColorId, string GarmentSize);

public interface IPublishedSkuReader
{
    Task<PublishedSkuVariant?> FindAsync(string skuId, CancellationToken cancellationToken = default);
}
