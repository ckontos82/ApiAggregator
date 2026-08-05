using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.Models
{
    /// <summary>
    /// A single result item normalized to a common shape, whichever
    /// external API it came from.
    /// </summary>
    public sealed record AggregatedItem
    {
        /// <summary>Stable identifier, prefixed with the source (e.g. "github:123").</summary>
        public required string Id { get; init; }

        /// <summary>The external API the item came from.</summary>
        public required AggregationSource Source { get; init; }

        /// <summary>The kind of content this item represents.</summary>
        public required ContentCategory Category { get; init; }

        /// <summary>Display title of the item.</summary>
        public required string Title { get; init; }

        /// <summary>Longer description, when the source provides one.</summary>
        public string? Description { get; init; }

        /// <summary>Link to the item on the source's own site.</summary>
        public string? Url { get; init; }

        /// <summary>When the item was created or published at the source.</summary>
        public required DateTimeOffset Timestamp { get; init; }
    }
}
