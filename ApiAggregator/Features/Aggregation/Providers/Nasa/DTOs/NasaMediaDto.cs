using System.Text.Json.Serialization;

namespace ApiAggregator.Features.Aggregation.Providers.Nasa.DTOs;

internal sealed record NasaMediaDto
{
    [JsonPropertyName("nasa_id")]
    public required string NasaId { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("date_created")]
    public required DateTimeOffset DateCreated { get; init; }
}
