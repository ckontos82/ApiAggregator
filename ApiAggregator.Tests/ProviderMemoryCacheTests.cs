using ApiAggregator.Features.Aggregation.Caching;
using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;

namespace ApiAggregator.Tests
{
    public sealed class ProviderMemoryCacheTests
    {
        private static readonly DateTimeOffset BaseTime =
            new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeTimeProvider _timeProvider = new(BaseTime);
        private readonly ProviderMemoryCache _cache;

        public ProviderMemoryCacheTests()
        {
            _cache = new ProviderMemoryCache(
                new MemoryCache(new MemoryCacheOptions()),
                _timeProvider);
        }

        [Fact]
        public void TryGetFresh_ReturnsEntry_WithinFreshLifetime()
        {
            _cache.Set("key", [CreateItem()]);

            _timeProvider.Advance(TimeSpan.FromSeconds(29));

            Assert.True(_cache.TryGetFresh("key", out var entry));
            Assert.Single(entry!.Items);
            Assert.Equal(BaseTime, entry.CachedAt);
        }

        [Fact]
        public void TryGetFresh_ReturnsFalse_AfterFreshLifetime()
        {
            _cache.Set("key", [CreateItem()]);

            _timeProvider.Advance(TimeSpan.FromSeconds(31));

            Assert.False(_cache.TryGetFresh("key", out _));
        }

        [Fact]
        public void TryGetStale_StillReturnsEntry_AfterFreshLifetime()
        {
            _cache.Set("key", [CreateItem()]);

            _timeProvider.Advance(TimeSpan.FromMinutes(5));

            Assert.True(_cache.TryGetStale("key", out var entry));
            Assert.Single(entry!.Items);
        }

        [Fact]
        public void TryGetFresh_ReturnsFalse_ForUnknownKey()
        {
            Assert.False(_cache.TryGetFresh("missing", out var entry));
            Assert.Null(entry);
        }

        [Fact]
        public void TryGetStale_ReturnsFalse_ForUnknownKey()
        {
            Assert.False(_cache.TryGetStale("missing", out var entry));
            Assert.Null(entry);
        }

        [Fact]
        public void Set_OverwritesExistingEntry()
        {
            _cache.Set("key", [CreateItem("first")]);

            _timeProvider.Advance(TimeSpan.FromSeconds(10));
            _cache.Set("key", [CreateItem("second")]);

            Assert.True(_cache.TryGetFresh("key", out var entry));
            Assert.Equal("github:second", Assert.Single(entry!.Items).Id);
            Assert.Equal(BaseTime.AddSeconds(10), entry.CachedAt);
        }

        private static AggregatedItem CreateItem(string id = "item")
        {
            return new AggregatedItem
            {
                Id = $"github:{id}",
                Source = AggregationSource.GitHub,
                Category = ContentCategory.Repository,
                Title = id,
                Timestamp = BaseTime
            };
        }
    }
}
