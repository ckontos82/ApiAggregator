using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers;

namespace ApiAggregator.Features.Aggregation.Services
{
    public sealed class AggregationService(IEnumerable<IAggregationProvider> providers, ILogger<AggregationService> logger) : IAggregationService
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

                selectedProviders = selectedProviders.Where(provider =>
                    requestedSources.Contains(provider.Source));
            }

            if (query.Category is { } category)
            {
                selectedProviders = selectedProviders.Where(provider =>
                    provider.Category == category);
            }

            return selectedProviders.ToArray();
        }

        private async Task<ProviderResult> ExecuteProviderAsync(
            IAggregationProvider provider,
            ProviderSearchRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var items = await provider.SearchAsync(
                    request,
                    cancellationToken);

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
                // The caller token was not cancelled, so this is treated
                // as the provider HttpClient timeout.
                logger.LogWarning(
                    exception,
                    "Provider {Provider} timed out.",
                    provider.Source);

                return new ProviderResult
                {
                    Source = provider.Source,
                    Items = [],
                    Status = ProviderStatus.Unavailable,
                    ErrorMessage =
                        $"{provider.Source} did not respond within the allowed time."
                };
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(
                    exception,
                    "HTTP request to provider {Provider} failed.",
                    provider.Source);

                return new ProviderResult
                {
                    Source = provider.Source,
                    Items = [],
                    Status = ProviderStatus.Unavailable,
                    ErrorMessage =
                        $"{provider.Source} is temporarily unavailable."
                };
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unexpected error while executing provider {Provider}.",
                    provider.Source);

                return new ProviderResult
                {
                    Source = provider.Source,
                    Items = [],
                    Status = ProviderStatus.Unavailable,
                    ErrorMessage =
                        $"{provider.Source} could not return results."
                };
            }
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
    }
}
