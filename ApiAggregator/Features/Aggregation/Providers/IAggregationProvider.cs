using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;

namespace ApiAggregator.Features.Aggregation.Providers
{
    public interface IAggregationProvider
    {
        AggregationSource Source { get; }
        ContentCategory Category { get; }
        Task<IReadOnlyList<AggregatedItem>> SearchAsync(
            ProviderSearchRequest request,
            CancellationToken cancellationToken);
    }
}
