using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.DTOs
{
    /// <summary>
    /// Request statistics accumulated for one external API since startup.
    /// </summary>
    public sealed record ProviderStatisticsDto
    {
        /// <summary>The external API these statistics describe.</summary>
        public required AggregationSource Source { get; init; }

        /// <summary>Total number of real external calls made (cache hits excluded).</summary>
        public required int TotalRequests { get; init; }

        /// <summary>Number of calls that completed successfully.</summary>
        public required int SuccessfulRequests { get; init; }

        /// <summary>Number of calls that failed or timed out.</summary>
        public required int FailedRequests { get; init; }

        /// <summary>Mean response time across all calls, in milliseconds.</summary>
        public required double AverageResponseTimeMs { get; init; }

        /// <summary>Call counts grouped by response-time bucket.</summary>
        public required PerformanceBucketsDto Buckets { get; init; }
    }
}
