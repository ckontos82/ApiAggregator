using System.Text.Json.Serialization;

namespace ApiAggregator.Features.Aggregation.Providers.GitHub.DTOs
{
    internal sealed record GitHubSearchResponse
    {
        [JsonPropertyName("items")]
        public List<GitHubRepositoryDto> Items { get; init; } = [];
    }
}
