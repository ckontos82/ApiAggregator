using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers.NewsApi.DTOs;
using System.Security.Cryptography;
using System.Text;

namespace ApiAggregator.Features.Aggregation.Providers.NewsApi
{
    /// <summary>
    /// Searches news articles via NewsAPI's /v2/everything endpoint. Item ids
    /// are derived from a SHA-256 hash of the article URL, since NewsAPI
    /// provides no stable identifier of its own.
    /// </summary>
    public sealed class NewsApiProvider(HttpClient httpClient) : IAggregationProvider
    {
        public AggregationSource Source => AggregationSource.NewsApi;

        public ContentCategory Category => ContentCategory.Article;

        public async Task<IReadOnlyList<AggregatedItem>> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
        {
            var requestUri = BuildRequestUri(request);
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                throw new HttpRequestException(
                    $"NewsAPI returned {(int)response.StatusCode}: {errorBody}",
                    null,
                    response.StatusCode);
            }

            var newsApiResponse = await response.Content.ReadFromJsonAsync<NewsApiSearchResponse>(cancellationToken: cancellationToken);

            if (newsApiResponse is null)
                throw new InvalidOperationException("NewsAPI returned an empty response");

            return newsApiResponse.Articles
                .Select(MapToAggregatedItem)
                .ToArray();
        }

        private static string FormatDate(DateOnly date)
            => date.ToString("yyyy-MM-dd");

        private static string BuildRequestUri(ProviderSearchRequest request)
        {
            var encodedQuery = Uri.EscapeDataString(request.Query.Trim());

            var requestUri =
                $"everything?q={encodedQuery}" +
                $"&pageSize={request.ResultsLimit}";

            if (request.FromDate is { } fromDate)
                requestUri += $"&from={FormatDate(fromDate)}";

            if (request.ToDate is { } toDate)
                requestUri += $"&to={FormatDate(toDate)}";

            return requestUri;
        }

        private static AggregatedItem MapToAggregatedItem(NewsApiDto articleDto)
            => new()
            {
                Id = CreateArticleId(articleDto.Url),
                Source = AggregationSource.NewsApi,
                Category = ContentCategory.Article,
                Title = articleDto.Title,
                Description = articleDto.Description,
                Url = articleDto.Url,
                Timestamp = articleDto.PublishedAt
            };

        private static string CreateArticleId(string url)
        {
            var normalizedUrl = url.Trim();

            var bytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(normalizedUrl));

            return $"newsapi:{Convert.ToHexString(bytes)}";
        }
    }
}
