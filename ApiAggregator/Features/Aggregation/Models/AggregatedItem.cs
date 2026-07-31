using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.Models
{
    public sealed record AggregatedItem
    {
        public required string Id { get; init; }
        public required AggregationSource Source { get; init; }
        public required ContentCategory Category { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public string? Url { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }
}
