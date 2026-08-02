using ApiAggregator.Features.Aggregation.Models;

namespace ApiAggregator.Features.Aggregation.DTOs
{
    public sealed record AggregationResponseDto
    {
        public required IReadOnlyList<AggregatedItem> Items { get; init; }
        public required IReadOnlyList<ProviderExecutionDto> Providers { get; init; }
        public required int TotalCount { get; init; }
    }
}
