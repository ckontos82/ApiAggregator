using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Statistics;

namespace ApiAggregator.Tests
{
    public sealed class ProviderStatisticsCollectorTests
    {
        private readonly ProviderStatisticsCollector _collector = new();

        [Fact]
        public void GetSnapshot_ReturnsEmpty_WhenNothingRecorded()
        {
            Assert.Empty(_collector.GetSnapshot());
        }

        [Theory]
        [InlineData(99, 1, 0, 0)]
        [InlineData(100, 0, 1, 0)]
        [InlineData(200, 0, 1, 0)]
        [InlineData(201, 0, 0, 1)]
        public void Record_AssignsPerformanceBucket_ByElapsedTime(
            int elapsedMs,
            int expectedFast,
            int expectedAverage,
            int expectedSlow)
        {
            _collector.Record(
                AggregationSource.GitHub,
                TimeSpan.FromMilliseconds(elapsedMs),
                succeeded: true);

            var statistics = Assert.Single(_collector.GetSnapshot());

            Assert.Equal(expectedFast, statistics.Buckets.Fast);
            Assert.Equal(expectedAverage, statistics.Buckets.Average);
            Assert.Equal(expectedSlow, statistics.Buckets.Slow);
        }

        [Fact]
        public void Record_AccumulatesTotalsAndAverage()
        {
            _collector.Record(AggregationSource.GitHub, TimeSpan.FromMilliseconds(50), succeeded: true);
            _collector.Record(AggregationSource.GitHub, TimeSpan.FromMilliseconds(150), succeeded: true);
            _collector.Record(AggregationSource.GitHub, TimeSpan.FromMilliseconds(400), succeeded: false);

            var statistics = Assert.Single(_collector.GetSnapshot());

            Assert.Equal(AggregationSource.GitHub, statistics.Source);
            Assert.Equal(3, statistics.TotalRequests);
            Assert.Equal(2, statistics.SuccessfulRequests);
            Assert.Equal(1, statistics.FailedRequests);
            Assert.Equal(200, statistics.AverageResponseTimeMs);
            Assert.Equal(1, statistics.Buckets.Fast);
            Assert.Equal(1, statistics.Buckets.Average);
            Assert.Equal(1, statistics.Buckets.Slow);
        }

        [Fact]
        public void Record_TracksSourcesIndependently()
        {
            _collector.Record(AggregationSource.GitHub, TimeSpan.FromMilliseconds(50), succeeded: true);
            _collector.Record(AggregationSource.Nasa, TimeSpan.FromMilliseconds(300), succeeded: true);

            var snapshot = _collector.GetSnapshot();

            Assert.Equal(2, snapshot.Count);

            var gitHub = Assert.Single(snapshot, s => s.Source == AggregationSource.GitHub);
            Assert.Equal(50, gitHub.AverageResponseTimeMs);
            Assert.Equal(1, gitHub.Buckets.Fast);

            var nasa = Assert.Single(snapshot, s => s.Source == AggregationSource.Nasa);
            Assert.Equal(300, nasa.AverageResponseTimeMs);
            Assert.Equal(1, nasa.Buckets.Slow);
        }
    }
}
