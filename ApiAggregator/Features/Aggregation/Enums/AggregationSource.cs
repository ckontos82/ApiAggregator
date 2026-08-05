namespace ApiAggregator.Features.Aggregation.Enums
{
    /// <summary>
    /// The external APIs the service can aggregate.
    /// </summary>
    public enum AggregationSource
    {
        /// <summary>NASA Image and Video Library.</summary>
        Nasa = 1,

        /// <summary>NewsAPI article search.</summary>
        NewsApi = 2,

        /// <summary>GitHub repository search.</summary>
        GitHub = 3
    }
}
