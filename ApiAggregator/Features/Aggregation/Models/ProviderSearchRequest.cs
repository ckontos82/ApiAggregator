namespace ApiAggregator.Features.Aggregation.Models
{
    public sealed record ProviderSearchRequest
    {
        public required string Query { get; init; }
        public DateOnly? FromDate { get; init; }
        public DateOnly? ToDate { get; init; }
        public required int ResultsLimit { get; init; }
    }
}