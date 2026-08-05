using ApiAggregator.Features.Aggregation.Caching;
using ApiAggregator.Features.Aggregation.Models;

namespace ApiAggregator.Tests.TestDoubles
{
    internal sealed class NoOpProviderCache : IProviderCache
    {
        public bool TryGetFresh(string key, out ProviderCacheEntry? entry)
        {
            entry = null;
            return false;
        }

        public bool TryGetStale(string key, out ProviderCacheEntry? entry)
        {
            entry = null;
            return false;
        }

        public void Set(string key, IReadOnlyList<AggregatedItem> items)
        {
        }
    }
}
