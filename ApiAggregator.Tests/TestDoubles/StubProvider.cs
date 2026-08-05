using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers;

namespace ApiAggregator.Tests.TestDoubles
{
    internal sealed class StubProvider : IAggregationProvider
    {
        public required AggregationSource Source { get; init; }

        public required ContentCategory Category { get; init; }

        public Func<ProviderSearchRequest, IReadOnlyList<AggregatedItem>> Handler { get; set; }
            = _ => [];

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<AggregatedItem>> SearchAsync(
            ProviderSearchRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Handler(request));
        }
    }
}
