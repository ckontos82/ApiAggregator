using ApiAggregator.Features.Aggregation.Models;

namespace ApiAggregator.Features.Aggregation.DTOs
{
    /// <summary>
    /// The merged result of one aggregated search.
    /// </summary>
    public sealed record AggregationResponseDto
    {
        /// <summary>Merged items from all providers, filtered and sorted as requested.</summary>
        public required IReadOnlyList<AggregatedItem> Items { get; init; }

        /// <summary>Execution outcome of each provider, including failures and cache usage.</summary>
        public required IReadOnlyList<ProviderExecutionDto> Providers { get; init; }

        /// <summary>Total number of items returned.</summary>
        public required int TotalCount { get; init; }
    }
}
