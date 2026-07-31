using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.Models
{
    public sealed record ProviderResult
    {
        public required AggregationSource Source { get; init; }
        public required IReadOnlyList<AggregatedItem> Items { get; init; }
        public required ProviderStatus Status { get; init; }
        public bool IsFromCache { get; init; }
        public bool IsStale { get; init; }
        public string? ErrorMessage { get; init; }
    }
}