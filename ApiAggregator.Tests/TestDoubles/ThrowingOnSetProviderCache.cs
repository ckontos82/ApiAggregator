using ApiAggregator.Features.Aggregation.Caching;
using ApiAggregator.Features.Aggregation.Models;

namespace ApiAggregator.Tests.TestDoubles;

/// <summary>
/// Always misses, and throws when a result is stored: simulates a failure
/// after the provider call itself has succeeded.
/// </summary>
internal sealed class ThrowingOnSetProviderCache : IProviderCache
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
        => throw new InvalidOperationException("Cache write failed.");
}
