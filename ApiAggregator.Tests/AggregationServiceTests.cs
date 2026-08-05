using ApiAggregator.Features.Aggregation;
using ApiAggregator.Features.Aggregation.Caching;
using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers;
using ApiAggregator.Features.Aggregation.Services;
using ApiAggregator.Features.Aggregation.Statistics;
using ApiAggregator.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ApiAggregator.Tests
{
    public sealed class AggregationServiceTests
    {
        private static readonly DateTimeOffset BaseTime =
            new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task AggregateAsync_MergesAllProviders_SortedByTimestampDescending()
        {
            var gitHub = CreateProvider(
                AggregationSource.GitHub,
                ContentCategory.Repository,
                CreateItem(AggregationSource.GitHub, "old", BaseTime.AddDays(-2)));

            var nasa = CreateProvider(
                AggregationSource.Nasa,
                ContentCategory.Media,
                CreateItem(AggregationSource.Nasa, "new", BaseTime));

            var service = CreateService([gitHub, nasa]);

            var response = await service.AggregateAsync(
                new AggregationQueryDto { Query = "apollo" },
                CancellationToken.None);

            Assert.Equal(2, response.TotalCount);
            Assert.Equal(["nasa:new", "github:old"], response.Items.Select(item => item.Id));
            Assert.All(response.Providers, provider =>
                Assert.Equal(ProviderStatus.Succeeded, provider.Status));
        }

        [Fact]
        public async Task AggregateAsync_SortsByTitleAscending_WhenRequested()
        {
            var gitHub = CreateProvider(
                AggregationSource.GitHub,
                ContentCategory.Repository,
                CreateItem(AggregationSource.GitHub, "1", BaseTime, title: "zebra"),
                CreateItem(AggregationSource.GitHub, "2", BaseTime, title: "Apple"));

            var service = CreateService([gitHub]);

            var response = await service.AggregateAsync(
                new AggregationQueryDto
                {
                    Query = "apollo",
                    SortBy = AggregationSortField.Title,
                    SortDirection = SortDirection.Ascending
                },
                CancellationToken.None);

            Assert.Equal(["Apple", "zebra"], response.Items.Select(item => item.Title));
        }

        [Fact]
        public async Task AggregateAsync_OnlyQueriesRequestedSources()
        {
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);
            var nasa = CreateProvider(AggregationSource.Nasa, ContentCategory.Media);

            var service = CreateService([gitHub, nasa]);

            var response = await service.AggregateAsync(
                new AggregationQueryDto
                {
                    Query = "apollo",
                    Sources = [AggregationSource.Nasa]
                },
                CancellationToken.None);

            Assert.Equal(0, gitHub.CallCount);
            Assert.Equal(1, nasa.CallCount);
            Assert.Equal(AggregationSource.Nasa, Assert.Single(response.Providers).Source);
        }

        [Fact]
        public async Task AggregateAsync_OnlyQueriesProvidersMatchingCategory()
        {
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);
            var nasa = CreateProvider(AggregationSource.Nasa, ContentCategory.Media);

            var service = CreateService([gitHub, nasa]);

            await service.AggregateAsync(
                new AggregationQueryDto
                {
                    Query = "apollo",
                    Category = ContentCategory.Repository
                },
                CancellationToken.None);

            Assert.Equal(1, gitHub.CallCount);
            Assert.Equal(0, nasa.CallCount);
        }

        [Fact]
        public async Task AggregateAsync_FiltersItemsOutsideDateRange()
        {
            var gitHub = CreateProvider(
                AggregationSource.GitHub,
                ContentCategory.Repository,
                CreateItem(AggregationSource.GitHub, "before", new DateTimeOffset(2026, 6, 30, 23, 59, 59, TimeSpan.Zero)),
                CreateItem(AggregationSource.GitHub, "inside", new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)),
                CreateItem(AggregationSource.GitHub, "after", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));

            var service = CreateService([gitHub]);

            var response = await service.AggregateAsync(
                new AggregationQueryDto
                {
                    Query = "apollo",
                    FromDate = new DateOnly(2026, 7, 1),
                    ToDate = new DateOnly(2026, 7, 31)
                },
                CancellationToken.None);

            Assert.Equal("github:inside", Assert.Single(response.Items).Id);
        }

        [Fact]
        public async Task AggregateAsync_UnregisteredSourceRequested_Throws()
        {
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);

            var service = CreateService([gitHub]);

            var exception = await Assert.ThrowsAsync<InvalidAggregationRequestException>(() =>
                service.AggregateAsync(
                    new AggregationQueryDto
                    {
                        Query = "apollo",
                        Sources = [AggregationSource.GitHub, AggregationSource.NewsApi]
                    },
                    CancellationToken.None));

            Assert.Contains("NewsApi", exception.Message);
        }

        [Fact]
        public async Task AggregateAsync_DisabledSourceRequested_ThrowsWithReason()
        {
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);
            var disabled = new DisabledProvider(
                AggregationSource.NewsApi,
                ContentCategory.Article,
                "NewsApi requires an API key.");

            var service = CreateService([gitHub], disabledProviders: [disabled]);

            var exception = await Assert.ThrowsAsync<InvalidAggregationRequestException>(() =>
                service.AggregateAsync(
                    new AggregationQueryDto
                    {
                        Query = "apollo",
                        Sources = [AggregationSource.NewsApi]
                    },
                    CancellationToken.None));

            Assert.Equal("NewsApi requires an API key.", exception.Message);
        }

        [Fact]
        public async Task AggregateAsync_CategoryWithoutProvider_Throws()
        {
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);

            var service = CreateService([gitHub]);

            var exception = await Assert.ThrowsAsync<InvalidAggregationRequestException>(() =>
                service.AggregateAsync(
                    new AggregationQueryDto
                    {
                        Query = "apollo",
                        Category = ContentCategory.Article
                    },
                    CancellationToken.None));

            Assert.Contains("Article", exception.Message);
        }

        [Fact]
        public async Task AggregateAsync_SourceAndCategoryMismatch_Throws()
        {
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);
            var nasa = CreateProvider(AggregationSource.Nasa, ContentCategory.Media);

            var service = CreateService([gitHub, nasa]);

            await Assert.ThrowsAsync<InvalidAggregationRequestException>(() =>
                service.AggregateAsync(
                    new AggregationQueryDto
                    {
                        Query = "apollo",
                        Sources = [AggregationSource.GitHub],
                        Category = ContentCategory.Media
                    },
                    CancellationToken.None));
        }

        [Fact]
        public async Task AggregateAsync_DisabledProviderListed_WhenNoSourcesRequested()
        {
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);
            var disabled = new DisabledProvider(
                AggregationSource.NewsApi,
                ContentCategory.Article,
                "NewsApi requires an API key.");

            var service = CreateService([gitHub], disabledProviders: [disabled]);

            var response = await service.AggregateAsync(
                new AggregationQueryDto { Query = "apollo" },
                CancellationToken.None);

            var newsApi = Assert.Single(
                response.Providers,
                provider => provider.Source == AggregationSource.NewsApi);

            Assert.Equal(ProviderStatus.Unavailable, newsApi.Status);
            Assert.Equal(0, newsApi.ItemCount);
            Assert.Equal("NewsApi requires an API key.", newsApi.ErrorMessage);
        }

        [Fact]
        public async Task AggregateAsync_DisabledProviderNotListed_WhenCategoryDiffers()
        {
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);
            var disabled = new DisabledProvider(
                AggregationSource.NewsApi,
                ContentCategory.Article,
                "NewsApi requires an API key.");

            var service = CreateService([gitHub], disabledProviders: [disabled]);

            var response = await service.AggregateAsync(
                new AggregationQueryDto
                {
                    Query = "apollo",
                    Category = ContentCategory.Repository
                },
                CancellationToken.None);

            Assert.DoesNotContain(
                response.Providers,
                provider => provider.Source == AggregationSource.NewsApi);
        }

        [Fact]
        public async Task AggregateAsync_FailingProvider_ReportedUnavailable_OthersStillSucceed()
        {
            var gitHub = CreateProvider(
                AggregationSource.GitHub,
                ContentCategory.Repository,
                CreateItem(AggregationSource.GitHub, "ok", BaseTime));

            var nasa = CreateProvider(AggregationSource.Nasa, ContentCategory.Media);
            nasa.Handler = _ => throw new HttpRequestException("boom");

            var service = CreateService([gitHub, nasa]);

            var response = await service.AggregateAsync(
                new AggregationQueryDto { Query = "apollo" },
                CancellationToken.None);

            var nasaExecution = Assert.Single(
                response.Providers,
                provider => provider.Source == AggregationSource.Nasa);

            Assert.Equal(ProviderStatus.Unavailable, nasaExecution.Status);
            Assert.Equal(0, nasaExecution.ItemCount);
            Assert.NotNull(nasaExecution.ErrorMessage);

            Assert.Equal("github:ok", Assert.Single(response.Items).Id);
        }

        [Fact]
        public async Task AggregateAsync_ProviderTimeout_ReportedUnavailable()
        {
            var nasa = CreateProvider(AggregationSource.Nasa, ContentCategory.Media);
            nasa.Handler = _ => throw new TaskCanceledException("HttpClient timeout");

            var service = CreateService([nasa]);

            var response = await service.AggregateAsync(
                new AggregationQueryDto { Query = "apollo" },
                CancellationToken.None);

            var execution = Assert.Single(response.Providers);
            Assert.Equal(ProviderStatus.Unavailable, execution.Status);
            Assert.Contains("did not respond", execution.ErrorMessage);
        }

        [Fact]
        public async Task AggregateAsync_CallerCancellation_Propagates()
        {
            using var cancellationSource = new CancellationTokenSource();

            var nasa = CreateProvider(AggregationSource.Nasa, ContentCategory.Media);
            nasa.Handler = _ =>
            {
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            };

            var service = CreateService([nasa]);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.AggregateAsync(
                    new AggregationQueryDto { Query = "apollo" },
                    cancellationSource.Token));
        }

        [Fact]
        public async Task AggregateAsync_FreshCacheHit_SkipsProviderCall()
        {
            var timeProvider = new FakeTimeProvider(BaseTime);
            var cache = CreateRealCache(timeProvider);

            var gitHub = CreateProvider(
                AggregationSource.GitHub,
                ContentCategory.Repository,
                CreateItem(AggregationSource.GitHub, "cached", BaseTime));

            var service = CreateService([gitHub], cache: cache);
            var query = new AggregationQueryDto { Query = "apollo" };

            await service.AggregateAsync(query, CancellationToken.None);
            var second = await service.AggregateAsync(query, CancellationToken.None);

            Assert.Equal(1, gitHub.CallCount);

            var execution = Assert.Single(second.Providers);
            Assert.True(execution.IsFromCache);
            Assert.False(execution.IsStale);
            Assert.Equal(ProviderStatus.Succeeded, execution.Status);
        }

        [Fact]
        public async Task AggregateAsync_FailureWithStaleCache_ReturnsDegradedStaleItems()
        {
            var timeProvider = new FakeTimeProvider(BaseTime);
            var cache = CreateRealCache(timeProvider);

            var gitHub = CreateProvider(
                AggregationSource.GitHub,
                ContentCategory.Repository,
                CreateItem(AggregationSource.GitHub, "cached", BaseTime));

            var service = CreateService([gitHub], cache: cache);
            var query = new AggregationQueryDto { Query = "apollo" };

            await service.AggregateAsync(query, CancellationToken.None);

            timeProvider.Advance(TimeSpan.FromMinutes(5));
            gitHub.Handler = _ => throw new HttpRequestException("down");

            var response = await service.AggregateAsync(query, CancellationToken.None);

            var execution = Assert.Single(response.Providers);
            Assert.Equal(ProviderStatus.Degraded, execution.Status);
            Assert.True(execution.IsFromCache);
            Assert.True(execution.IsStale);
            Assert.NotNull(execution.ErrorMessage);

            Assert.Equal("github:cached", Assert.Single(response.Items).Id);
        }

        [Fact]
        public async Task AggregateAsync_RecordsSuccessStatistics_ForRealProviderCall()
        {
            var collector = new RecordingStatisticsCollector();
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);

            var service = CreateService([gitHub], statisticsCollector: collector);

            await service.AggregateAsync(
                new AggregationQueryDto { Query = "apollo" },
                CancellationToken.None);

            var record = Assert.Single(collector.Records);
            Assert.Equal(AggregationSource.GitHub, record.Source);
            Assert.True(record.Succeeded);
        }

        [Fact]
        public async Task AggregateAsync_RecordsFailureStatistics_WhenProviderThrows()
        {
            var collector = new RecordingStatisticsCollector();
            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);
            gitHub.Handler = _ => throw new HttpRequestException("down");

            var service = CreateService([gitHub], statisticsCollector: collector);

            await service.AggregateAsync(
                new AggregationQueryDto { Query = "apollo" },
                CancellationToken.None);

            var record = Assert.Single(collector.Records);
            Assert.Equal(AggregationSource.GitHub, record.Source);
            Assert.False(record.Succeeded);
        }

        [Fact]
        public async Task AggregateAsync_DoesNotRecordStatistics_OnFreshCacheHit()
        {
            var collector = new RecordingStatisticsCollector();
            var timeProvider = new FakeTimeProvider(BaseTime);
            var cache = CreateRealCache(timeProvider);

            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);

            var service = CreateService(
                [gitHub],
                cache: cache,
                statisticsCollector: collector,
                timeProvider: timeProvider);

            var query = new AggregationQueryDto { Query = "apollo" };

            await service.AggregateAsync(query, CancellationToken.None);
            await service.AggregateAsync(query, CancellationToken.None);

            Assert.Single(collector.Records);
        }

        [Fact]
        public async Task AggregateAsync_MeasuresElapsedTime_UsingTimeProvider()
        {
            var collector = new RecordingStatisticsCollector();
            var timeProvider = new FakeTimeProvider(BaseTime);

            var gitHub = CreateProvider(AggregationSource.GitHub, ContentCategory.Repository);
            gitHub.Handler = _ =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(150));
                return [];
            };

            var service = CreateService(
                [gitHub],
                statisticsCollector: collector,
                timeProvider: timeProvider);

            await service.AggregateAsync(
                new AggregationQueryDto { Query = "apollo" },
                CancellationToken.None);

            var record = Assert.Single(collector.Records);
            Assert.Equal(TimeSpan.FromMilliseconds(150), record.Elapsed);
        }

        private static AggregationService CreateService(
            IReadOnlyList<IAggregationProvider> providers,
            IReadOnlyList<DisabledProvider>? disabledProviders = null,
            IProviderCache? cache = null,
            IProviderStatisticsCollector? statisticsCollector = null,
            TimeProvider? timeProvider = null)
        {
            return new AggregationService(
                providers,
                disabledProviders ?? [],
                cache ?? new NoOpProviderCache(),
                statisticsCollector ?? new RecordingStatisticsCollector(),
                timeProvider ?? TimeProvider.System,
                NullLogger<AggregationService>.Instance);
        }

        private static IProviderCache CreateRealCache(FakeTimeProvider timeProvider)
        {
            return new ProviderMemoryCache(
                new MemoryCache(new MemoryCacheOptions()),
                timeProvider);
        }

        private static StubProvider CreateProvider(
            AggregationSource source,
            ContentCategory category,
            params AggregatedItem[] items)
        {
            return new StubProvider
            {
                Source = source,
                Category = category,
                Handler = _ => items
            };
        }

        private static AggregatedItem CreateItem(
            AggregationSource source,
            string id,
            DateTimeOffset timestamp,
            string? title = null)
        {
            var category = source switch
            {
                AggregationSource.GitHub => ContentCategory.Repository,
                AggregationSource.Nasa => ContentCategory.Media,
                _ => ContentCategory.Article
            };

            return new AggregatedItem
            {
                Id = $"{source.ToString().ToLowerInvariant()}:{id}",
                Source = source,
                Category = category,
                Title = title ?? id,
                Timestamp = timestamp
            };
        }
    }
}
