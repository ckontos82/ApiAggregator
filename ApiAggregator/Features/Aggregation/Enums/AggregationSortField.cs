namespace ApiAggregator.Features.Aggregation.Enums
{
    /// <summary>
    /// Fields the merged results can be sorted by.
    /// </summary>
    public enum AggregationSortField
    {
        /// <summary>Item creation/publication time.</summary>
        Timestamp = 1,

        /// <summary>Item title, case-insensitive.</summary>
        Title = 2,

        /// <summary>Source name.</summary>
        Source = 3,

        /// <summary>Content category name.</summary>
        Category = 4
    }
}
