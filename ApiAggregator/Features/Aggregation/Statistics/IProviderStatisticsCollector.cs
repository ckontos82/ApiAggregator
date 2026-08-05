using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.Statistics
{
    /// <summary>
    /// Accumulates per-provider request statistics (counts, response times,
    /// performance buckets) in memory for the statistics endpoint.
    /// </summary>
    public interface IProviderStatisticsCollector
    {
        /// <summary>Records one completed external call.</summary>
        void Record(AggregationSource source, TimeSpan elapsed, bool succeeded);

        /// <summary>Returns a consistent snapshot of the statistics gathered so far.</summary>
        IReadOnlyList<ProviderStatisticsDto> GetSnapshot();
    }
}
