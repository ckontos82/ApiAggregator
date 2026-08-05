namespace ApiAggregator.Features.Aggregation.Enums
{
    /// <summary>
    /// Outcome of one provider call within an aggregated search.
    /// </summary>
    public enum ProviderStatus
    {
        /// <summary>The provider returned results normally (live or fresh cache).</summary>
        Succeeded = 1,

        /// <summary>The provider failed, but stale cached results were served as a fallback.</summary>
        Degraded = 2,

        /// <summary>The provider failed and no cached results were available.</summary>
        Unavailable = 3
    }
}
