using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Statistics;

namespace ApiAggregator.Tests.TestDoubles
{
    internal sealed class RecordingStatisticsCollector : IProviderStatisticsCollector
    {
        public List<(AggregationSource Source, TimeSpan Elapsed, bool Succeeded)> Records { get; } = [];

        public void Record(AggregationSource source, TimeSpan elapsed, bool succeeded)
        {
            Records.Add((source, elapsed, succeeded));
        }

        public IReadOnlyList<ProviderStatisticsDto> GetSnapshot() => [];
    }
}
