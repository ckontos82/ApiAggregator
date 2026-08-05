using ApiAggregator.Features.Aggregation.DTOs;

namespace ApiAggregator.Features.Aggregation.Services
{
    /// <summary>
    /// Orchestrates one aggregated search: selects providers, runs them in
    /// parallel with caching and fallback, then merges, filters, and sorts
    /// the results.
    /// </summary>
    public interface IAggregationService
    {
        /// <summary>Runs the aggregated search described by <paramref name="query"/>.</summary>
        /// <exception cref="InvalidAggregationRequestException">
        /// The query names a source that is not available, or a source/category
        /// combination that matches no provider.
        /// </exception>
        Task<AggregationResponseDto> AggregateAsync(AggregationQueryDto query, CancellationToken cancellationToken);
    }
}
