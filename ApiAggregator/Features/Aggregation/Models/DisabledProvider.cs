using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.Models
{
    /// <summary>
    /// Marker registered in place of a provider that could not be enabled
    /// (e.g. missing API key), carrying the reason so responses and
    /// validation errors can explain the absence.
    /// </summary>
    /// <param name="Source">The source that is unavailable.</param>
    /// <param name="Category">The content category the provider would have served.</param>
    /// <param name="Reason">Human-readable explanation, including how to enable the provider.</param>
    public sealed record DisabledProvider(
        AggregationSource Source,
        ContentCategory Category,
        string Reason);
}
