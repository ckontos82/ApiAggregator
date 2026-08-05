using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.Models
{
    /// <summary>
    /// Internal outcome of executing one provider, including cache and
    /// failure details, before mapping to the response DTO.
    /// </summary>
    public sealed record ProviderResult
    {
        /// <summary>The external API this result came from.</summary>
        public required AggregationSource Source { get; init; }

        /// <summary>The items the provider contributed (empty on failure without fallback).</summary>
        public required IReadOnlyList<AggregatedItem> Items { get; init; }

        /// <summary>Outcome of the call.</summary>
        public required ProviderStatus Status { get; init; }

        /// <summary>True when the items came from the cache instead of a live call.</summary>
        public bool IsFromCache { get; init; }

        /// <summary>True when the cached items had expired but were served as a fallback.</summary>
        public bool IsStale { get; init; }

        /// <summary>Explanation of the failure, when the provider did not succeed.</summary>
        public string? ErrorMessage { get; init; }
    }
}
