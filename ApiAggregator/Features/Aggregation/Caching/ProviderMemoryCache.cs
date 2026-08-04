using ApiAggregator.Features.Aggregation.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ApiAggregator.Features.Aggregation.Caching
{

    internal sealed class ProviderMemoryCache(IMemoryCache cache, TimeProvider timeProvider) : IProviderCache
    {
        private static readonly TimeSpan FreshLifetime = TimeSpan.FromSeconds(30);

        private static readonly TimeSpan StaleLifetime = TimeSpan.FromMinutes(30);

        public bool TryGetFresh(string key, out ProviderCacheEntry? entry)
        {
            if (!TryGetStale(key, out entry))
                return false;

            return timeProvider.GetUtcNow() - entry!.CachedAt <= FreshLifetime;
        }

        public bool TryGetStale(string key, out ProviderCacheEntry? entry)
        {
            return cache.TryGetValue(key, out entry)
                && entry is not null;
        }

        public void Set(string key, IReadOnlyList<AggregatedItem> items)
        {
            var entry = new ProviderCacheEntry
            {
                Items = items,
                CachedAt = timeProvider.GetUtcNow()
            };

            cache.Set(
                key,
                entry,
                StaleLifetime);
        }
    }
}
