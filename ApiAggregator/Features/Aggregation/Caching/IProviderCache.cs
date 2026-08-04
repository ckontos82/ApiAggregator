using ApiAggregator.Features.Aggregation.Models;

namespace ApiAggregator.Features.Aggregation.Caching
{
    internal interface IProviderCache
    {
        bool TryGetFresh(string key, out ProviderCacheEntry? entry);

        bool TryGetStale(string key, out ProviderCacheEntry? entry);

        void Set(string key, IReadOnlyList<AggregatedItem> items);
    }
}
