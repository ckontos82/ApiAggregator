namespace ApiAggregator.Features.Aggregation.Enums
{
    /// <summary>
    /// The kind of content a provider returns.
    /// </summary>
    public enum ContentCategory
    {
        /// <summary>Images and videos.</summary>
        Media = 1,

        /// <summary>News articles.</summary>
        Article = 2,

        /// <summary>Source code repositories.</summary>
        Repository = 3
    }
}
