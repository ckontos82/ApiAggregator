using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers.GitHub.DTOs;
using System.Globalization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ApiAggregator.Features.Aggregation.Providers.GitHub
{
    public class GitHubProvider(HttpClient httpClient) : IAggregationProvider
    {
        public AggregationSource AggregationSource => AggregationSource.GitHub;

        public ContentCategory Category => ContentCategory.Repository;

        public async Task<IReadOnlyList<AggregatedItem>> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
        {
            var requestUri = BuildRequestUri(request);
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);

            response.EnsureSuccessStatusCode();

            var githubResponse = await response.Content.ReadFromJsonAsync<GitHubSearchResponse>(cancellationToken: cancellationToken);

            if (githubResponse is null)
                throw new InvalidOperationException("GitHub returned an empty response");

            return githubResponse.Items
                .Select(MapToAggregatedItem)
                .ToArray();
        }

        private static string BuildRequestUri(ProviderSearchRequest request)
        {
            var searchTerms = new List<string>
            {
                request.Query.Trim()
            };

            if (request.FromDate is { } fromDate)
            {
                searchTerms.Add(
                    $"created:>={FormatDate(fromDate)}");
            }

            if (request.ToDate is { } toDate)
            {
                searchTerms.Add(
                    $"created:<={FormatDate(toDate)}");
            }

            var searchExpression = string.Join(' ', searchTerms);
            var encodedQuery = Uri.EscapeDataString(searchExpression);

            return $"search/repositories" +
                   $"?q={encodedQuery}" +
                   $"&per_page={request.ResultsLimit}";
        }

        private static string FormatDate(DateOnly date)
            => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static AggregatedItem MapToAggregatedItem(GitHubRepositoryDto repositoryDto)
            => new()
            { 
                Id = $"github:{repositoryDto.Id}",
                Source = AggregationSource.GitHub,
                Category = ContentCategory.Repository,
                Title = repositoryDto.FullName,
                Description = repositoryDto.Description,
                Url = repositoryDto.HtmlUrl,
                Timestamp = repositoryDto.CreatedAt
            };
    }
}
