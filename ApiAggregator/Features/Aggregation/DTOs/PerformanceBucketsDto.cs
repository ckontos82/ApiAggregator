namespace ApiAggregator.Features.Aggregation.DTOs
{
    /// <summary>
    /// Call counts grouped by response time.
    /// </summary>
    public sealed record PerformanceBucketsDto
    {
        /// <summary>Calls that completed in under 100 ms.</summary>
        public required int Fast { get; init; }

        /// <summary>Calls that completed in 100-200 ms.</summary>
        public required int Average { get; init; }

        /// <summary>Calls that took longer than 200 ms.</summary>
        public required int Slow { get; init; }
    }
}
