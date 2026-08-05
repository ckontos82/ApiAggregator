using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Enums;
using System.Collections.Concurrent;

namespace ApiAggregator.Features.Aggregation.Statistics
{
    internal sealed class ProviderStatisticsCollector : IProviderStatisticsCollector
    {
        // Indicative thresholds from the assignment: fast < 100 ms,
        // average 100-200 ms, slow > 200 ms.
        private static readonly TimeSpan FastUpperBound = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan AverageUpperBound = TimeSpan.FromMilliseconds(200);

        private readonly ConcurrentDictionary<AggregationSource, SourceStatistics> _statistics = new();

        public void Record(AggregationSource source, TimeSpan elapsed, bool succeeded)
        {
            var sourceStatistics = _statistics.GetOrAdd(
                source,
                _ => new SourceStatistics());

            lock (sourceStatistics)
            {
                sourceStatistics.TotalRequests++;
                sourceStatistics.TotalElapsed += elapsed;

                if (succeeded)
                {
                    sourceStatistics.SuccessfulRequests++;
                }
                else
                {
                    sourceStatistics.FailedRequests++;
                }

                if (elapsed < FastUpperBound)
                {
                    sourceStatistics.FastCount++;
                }
                else if (elapsed <= AverageUpperBound)
                {
                    sourceStatistics.AverageCount++;
                }
                else
                {
                    sourceStatistics.SlowCount++;
                }
            }
        }

        public IReadOnlyList<ProviderStatisticsDto> GetSnapshot()
        {
            return _statistics
                .OrderBy(pair => pair.Key)
                .Select(pair => MapToDto(pair.Key, pair.Value))
                .ToArray();
        }

        private static ProviderStatisticsDto MapToDto(
            AggregationSource source,
            SourceStatistics sourceStatistics)
        {
            lock (sourceStatistics)
            {
                return new ProviderStatisticsDto
                {
                    Source = source,
                    TotalRequests = sourceStatistics.TotalRequests,
                    SuccessfulRequests = sourceStatistics.SuccessfulRequests,
                    FailedRequests = sourceStatistics.FailedRequests,
                    AverageResponseTimeMs = Math.Round(
                        sourceStatistics.TotalElapsed.TotalMilliseconds
                            / sourceStatistics.TotalRequests,
                        2),
                    Buckets = new PerformanceBucketsDto
                    {
                        Fast = sourceStatistics.FastCount,
                        Average = sourceStatistics.AverageCount,
                        Slow = sourceStatistics.SlowCount
                    }
                };
            }
        }

        private sealed class SourceStatistics
        {
            public int TotalRequests;
            public int SuccessfulRequests;
            public int FailedRequests;
            public TimeSpan TotalElapsed;
            public int FastCount;
            public int AverageCount;
            public int SlowCount;
        }
    }
}
