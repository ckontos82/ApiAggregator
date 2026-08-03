using System.Text.Json.Serialization;

namespace ApiAggregator.Features.Aggregation.Providers.NewsApi.DTOs
{
    internal sealed record NewsApiSearchResponse
    {
        [JsonPropertyName("articles")]
        public List<NewsApiDto> Articles { get; init; } = [];
    }
}
