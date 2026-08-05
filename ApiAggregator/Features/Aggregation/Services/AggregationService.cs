using ApiAggregator.Features.Aggregation.Caching;
using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers;
using ApiAggregator.Features.Aggregation.Statistics;

namespace ApiAggregator.Features.Aggregation.Services
{
    internal sealed class AggregationService(
        IEnumerable<IAggregationProvider> providers,
        IEnumerable<DisabledProvider> disabledProviders,
        IProviderCache providerCache,
        IProviderStatisticsCollector statisticsCollector,
        TimeProvider timeProvider,
        ILogger<AggregationService> logger) : IAggregationService
    {
        public async Task<AggregationResponseDto> AggregateAsync(AggregationQueryDto query, CancellationToken cancellationToken)
        {
            var selectedProviders = SelectProviders(query);

            var providerRequest = new ProviderSearchRequest
            {
                Query = query.Query.Trim(),
                FromDate = query.FromDate,
                ToDate = query.ToDate,
                ResultsLimit = query.ResultsPerSource
            };

            var providerTasks = selectedProviders.Select(provider => 
                ExecuteProviderAsync(provider, providerRequest, cancellationToken));

            var providerResults = await Task.WhenAll(providerTasks);

            var aggregatedItems = providerResults
                .SelectMany(result => result.Items);

            var filteredItems = ApplyFilters(aggregatedItems, query); 

            var sortedItems = ApplySorting(filteredItems, query.SortBy, query.SortDirection)
                .ToArray();

            var providerExecutions = providerResults
                .Select(MapProviderExecution)
                .Concat(SelectDisabledExecutions(query))
                .ToArray();

            return new AggregationResponseDto
            {
                Items = sortedItems,
                Providers = providerExecutions,
                TotalCount = sortedItems.Length
            };
        }

        private static IEnumerable<AggregatedItem> ApplySorting(
            IEnumerable<AggregatedItem> items, 
            AggregationSortField sortBy, 
            SortDirection sortDirection)
        {
            return (sortBy, sortDirection) switch
            {
                (AggregationSortField.Timestamp, SortDirection.Ascending) =>
                    items.OrderBy(item => item.Timestamp),

                (AggregationSortField.Timestamp, SortDirection.Descending) =>
                    items.OrderByDescending(item => item.Timestamp),

                (AggregationSortField.Title, SortDirection.Ascending) =>
                    items.OrderBy(
                        item => item.Title,
                        StringComparer.OrdinalIgnoreCase),

                (AggregationSortField.Title, SortDirection.Descending) =>
                    items.OrderByDescending(
                        item => item.Title,
                        StringComparer.OrdinalIgnoreCase),

                (AggregationSortField.Source, SortDirection.Ascending) =>
                    items.OrderBy(
                        item => item.Source.ToString(),
                        StringComparer.OrdinalIgnoreCase),

                (AggregationSortField.Source, SortDirection.Descending) =>
                    items.OrderByDescending(
                        item => item.Source.ToString(),
                        StringComparer.OrdinalIgnoreCase),

                (AggregationSortField.Category, SortDirection.Ascending) =>
                    items.OrderBy(
                        item => item.Category.ToString(),
                        StringComparer.OrdinalIgnoreCase),

                (AggregationSortField.Category, SortDirection.Descending) =>
                    items.OrderByDescending(
                        item => item.Category.ToString(),
                        StringComparer.OrdinalIgnoreCase),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(sortBy),
                    sortBy,
                    "The requested sorting option is unsupported.")
            };
        }

        private static IEnumerable<AggregatedItem> ApplyFilters(IEnumerable<AggregatedItem> items, AggregationQueryDto query)
        {
            if (query.Category is { } category)
            {
                items = items
                    .Where(item => item.Category == category);
            }

            if (query.FromDate is { } fromDate)
            {
                var fromTimestamp = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
                items = items
                    .Where(item => item.Timestamp >= fromTimestamp);
            }

            if (query.ToDate is { } toDate)
            {
                var exclusiveUpperBound = new DateTimeOffset(toDate
                        .AddDays(1)
                        .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

                items = items
                    .Where(item => item.Timestamp < exclusiveUpperBound);
            }

            return items;
        }

        private IReadOnlyList<IAggregationProvider> SelectProviders(AggregationQueryDto query)
        {
            IEnumerable<IAggregationProvider> selectedProviders = providers;

            if (query.Sources is { Length: > 0 })
            {
                var requestedSources = query.Sources.ToHashSet();

                var unavailableSources = requestedSources
                    .Except(providers.Select(provider => provider.Source))
                    .ToArray();

                if (unavailableSources.Length > 0)
                {
                    var reasons = unavailableSources.Select(source =>
                        disabledProviders
                            .FirstOrDefault(disabled => disabled.Source == source)
                            ?.Reason
                        ?? $"{source} is not available on this server.");

                    throw new InvalidAggregationRequestException(
                        string.Join(" ", reasons));
                }

                selectedProviders = selectedProviders.Where(provider =>
                    requestedSources.Contains(provider.Source));
            }

            if (query.Category is { } category)
            {
                selectedProviders = selectedProviders.Where(provider =>
                    provider.Category == category);
            }

            var selection = selectedProviders.ToArray();

            if (selection.Length == 0)
            {
                throw new InvalidAggregationRequestException(
                    query.Sources is { Length: > 0 }
                        ? $"None of the requested sources provide " +
                          $"'{query.Category}' content."
                        : $"No available source provides " +
                          $"'{query.Category}' content.");
            }

            return selection;
        }

        private async Task<ProviderResult> ExecuteProviderAsync(
            IAggregationProvider provider,
            ProviderSearchRequest request,
            CancellationToken cancellationToken)
        {
            var cacheKey = BuildCacheKey(provider, request);

            if (providerCache.TryGetFresh(cacheKey, out var cachedEntry))
            {
                return new ProviderResult
                {
                    Source = provider.Source,
                    Items = cachedEntry!.Items,
                    Status = ProviderStatus.Succeeded,
                    IsFromCache = true,
                    IsStale = false
                };
            }

            var startTimestamp = timeProvider.GetTimestamp();

            try
            {
                var items = await provider.SearchAsync(
                    request,
                    cancellationToken);

                RecordStatistics(provider, startTimestamp, succeeded: true);

                providerCache.Set(cacheKey, items);

                return new ProviderResult
                {
                    Source = provider.Source,
                    Items = items,
                    Status = ProviderStatus.Succeeded
                };
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // The caller cancelled the incoming HTTP request.
                throw;
            }
            catch (OperationCanceledException exception)
            {
                RecordStatistics(provider, startTimestamp, succeeded: false);

                // The caller token was not cancelled, so this is treated
                // as the provider HttpClient timeout.
                logger.LogWarning(
                    exception,
                    "Provider {Provider} timed out.",
                    provider.Source);

                return CreateFailureResult(provider, cacheKey, $"{provider.Source} did not respond within the allowed time.");
            }
            catch (HttpRequestException exception)
            {
                RecordStatistics(provider, startTimestamp, succeeded: false);

                logger.LogWarning(
                    exception,
                    "HTTP request to provider {Provider} failed.",
                    provider.Source);

                return CreateFailureResult(provider, cacheKey, $"{provider.Source} is temporarily unavailable.");
            }
            catch (Exception exception)
            {
                RecordStatistics(provider, startTimestamp, succeeded: false);

                logger.LogError(
                    exception,
                    "Unexpected error while executing provider {Provider}.",
                    provider.Source);

                return CreateFailureResult(provider, cacheKey, $"{provider.Source} could not return results.");
            }
        }

        private IEnumerable<ProviderExecutionDto> SelectDisabledExecutions(AggregationQueryDto query)
        {
            // Explicitly requested disabled sources were already rejected
            // with a validation error in SelectProviders.
            if (query.Sources is { Length: > 0 })
                yield break;

            foreach (var disabledProvider in disabledProviders)
            {
                if (query.Category is { } category
                    && disabledProvider.Category != category)
                {
                    continue;
                }

                yield return new ProviderExecutionDto
                {
                    Source = disabledProvider.Source,
                    Status = ProviderStatus.Unavailable,
                    ItemCount = 0,
                    ErrorMessage = disabledProvider.Reason
                };
            }
        }

        private void RecordStatistics(
            IAggregationProvider provider,
            long startTimestamp,
            bool succeeded)
        {
            statisticsCollector.Record(
                provider.Source,
                timeProvider.GetElapsedTime(startTimestamp),
                succeeded);
        }

        private static ProviderExecutionDto MapProviderExecution(ProviderResult result)
        {
            return new ProviderExecutionDto
            {
                Source = result.Source,
                Status = result.Status,
                ItemCount = result.Items.Count,
                IsFromCache = result.IsFromCache,
                IsStale = result.IsStale,
                ErrorMessage = result.ErrorMessage
            };
        }

        private static string BuildCacheKey(IAggregationProvider provider, ProviderSearchRequest request)
        {
            return string.Join(
                '|',
                provider.Source,
                request.Query.Trim().ToUpperInvariant(),
                request.FromDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                request.ToDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                request.ResultsLimit);
        }

        private ProviderResult CreateFailureResult(
            IAggregationProvider provider,
            string cacheKey,
            string errorMessage)
        {
            if (providerCache.TryGetStale(cacheKey, out var staleEntry))
            {
                return new ProviderResult
                {
                    Source = provider.Source,
                    Items = staleEntry!.Items,
                    Status = ProviderStatus.Degraded,
                    IsFromCache = true,
                    IsStale = true,
                    ErrorMessage = errorMessage
                };
            }

            return new ProviderResult
            {
                Source = provider.Source,
                Items = [],
                Status = ProviderStatus.Unavailable,
                ErrorMessage = errorMessage
            };
        }
    }
}
