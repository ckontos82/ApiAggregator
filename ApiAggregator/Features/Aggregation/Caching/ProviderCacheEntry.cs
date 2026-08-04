using ApiAggregator.Features.Aggregation.Models;

namespace ApiAggregator.Features.Aggregation.Caching
{
    internal sealed record ProviderCacheEntry
    {
        public required IReadOnlyList<AggregatedItem> Items { get; init; }

        public required DateTimeOffset CachedAt { get; init; }
    }
}
