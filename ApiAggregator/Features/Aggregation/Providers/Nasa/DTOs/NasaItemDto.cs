using System.Text.Json.Serialization;

namespace ApiAggregator.Features.Aggregation.Providers.Nasa.DTOs;

internal sealed record NasaItemDto
{
    [JsonPropertyName("data")]
    public List<NasaMediaDto> Data { get; init; } = [];
}
