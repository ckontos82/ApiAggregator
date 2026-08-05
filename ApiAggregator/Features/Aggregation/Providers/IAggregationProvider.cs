using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;

namespace ApiAggregator.Features.Aggregation.Providers
{
    /// <summary>
    /// Adapter for one external API. Implementations translate the common
    /// search request into the API's own protocol and map the results back
    /// to <see cref="AggregatedItem"/>. Register new providers in
    /// AggregationServiceCollectionExtensions to add them to the aggregation.
    /// </summary>
    public interface IAggregationProvider
    {
        /// <summary>The source this provider represents.</summary>
        AggregationSource Source { get; }

        /// <summary>The kind of content this provider returns.</summary>
        ContentCategory Category { get; }

        /// <summary>Executes the search against the external API.</summary>
        Task<IReadOnlyList<AggregatedItem>> SearchAsync(
            ProviderSearchRequest request,
            CancellationToken cancellationToken);
    }
}
