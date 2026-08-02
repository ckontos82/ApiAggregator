using ApiAggregator.Features.Aggregation.DTOs;

namespace ApiAggregator.Features.Aggregation.Services
{
    public interface IAggregationService
    {
        Task<AggregationResponseDto> AggregateAsync(AggregationQueryDto query, CancellationToken cancellationToken);
    }
}