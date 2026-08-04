using ApiAggregator.Features.Aggregation.Enums;

namespace ApiAggregator.Features.Aggregation.Models
{
    public sealed record DisabledProvider(
        AggregationSource Source,
        ContentCategory Category,
        string Reason);
}
