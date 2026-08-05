using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.DTOs
{
    /// <summary>
    /// How a single provider fared during one aggregated search.
    /// </summary>
    public sealed record ProviderExecutionDto
    {
        /// <summary>The external API this entry describes.</summary>
        public required AggregationSource Source { get; init; }

        /// <summary>Outcome of the call: succeeded, degraded (stale cache served), or unavailable.</summary>
        public required ProviderStatus Status { get; init; }

        /// <summary>Number of items this provider contributed before filtering.</summary>
        public required int ItemCount { get; init; }

        /// <summary>True when the items were served from the cache instead of a live call.</summary>
        public bool IsFromCache { get; init; }

        /// <summary>True when the cached items had expired but were served as a fallback.</summary>
        public bool IsStale { get; init; }

        /// <summary>Explanation of the failure, when the provider did not succeed.</summary>
        public string? ErrorMessage { get; init; }
    }
}
