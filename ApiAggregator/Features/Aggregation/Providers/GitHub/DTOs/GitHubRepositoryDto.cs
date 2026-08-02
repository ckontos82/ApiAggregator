using System.Text.Json.Serialization;

namespace ApiAggregator.Features.Aggregation.Providers.GitHub.DTOs
{
    internal sealed record GitHubRepositoryDto
    {
        [JsonPropertyName("id")]
        public required long Id { get; init; }

        [JsonPropertyName("full_name")]
        public required string FullName { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("html_url")]
        public required string HtmlUrl { get; init; }

        [JsonPropertyName("created_at")]
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
