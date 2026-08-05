namespace ApiAggregator.Features.Aggregation.Models
{
    /// <summary>
    /// The provider-agnostic search request handed to every
    /// <see cref="Providers.IAggregationProvider"/>.
    /// </summary>
    public sealed record ProviderSearchRequest
    {
        /// <summary>Search term to send to the external API.</summary>
        public required string Query { get; init; }

        /// <summary>Earliest item date to request (inclusive), when supported by the API.</summary>
        public DateOnly? FromDate { get; init; }

        /// <summary>Latest item date to request (inclusive), when supported by the API.</summary>
        public DateOnly? ToDate { get; init; }

        /// <summary>Maximum number of items to request from the API.</summary>
        public required int ResultsLimit { get; init; }
    }
}
