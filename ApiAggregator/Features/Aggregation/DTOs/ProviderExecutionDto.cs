using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.DTOs
{
    public sealed record ProviderExecutionDto
    {
        public required AggregationSource Source { get; init; }
        public required ProviderStatus Status { get; init; }
        public required int ItemCount { get; init; }
        public bool IsFromCache { get; init; }
        public bool IsStale { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
