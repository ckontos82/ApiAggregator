using System.Text.Json.Serialization;

namespace ApiAggregator.Features.Aggregation.Providers.NewsApi.DTOs
{
    internal sealed record NewsApiDto
    {
        [JsonPropertyName("url")]
        public required string Url { get; init; }

        [JsonPropertyName("title")]
        public required string Title { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("publishedAt")]
        public required DateTimeOffset PublishedAt { get; init; }
    }
}
