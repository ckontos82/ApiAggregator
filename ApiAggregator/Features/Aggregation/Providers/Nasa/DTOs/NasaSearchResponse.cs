using System.Text.Json.Serialization;

namespace ApiAggregator.Features.Aggregation.Providers.Nasa.DTOs;

internal sealed record NasaSearchResponse
{
    [JsonPropertyName("collection")]
    public NasaCollectionDto Collection { get; init; } = new();
}

internal sealed record NasaCollectionDto
{
    [JsonPropertyName("items")]
    public List<NasaItemDto> Items { get; init; } = [];
}
