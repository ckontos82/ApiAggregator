using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers.Nasa.DTOs;
using System.Globalization;

namespace ApiAggregator.Features.Aggregation.Providers.Nasa;

/// <summary>
/// Searches the NASA Image and Video Library. Date filters are applied at
/// year granularity, the finest the API supports.
/// </summary>
public sealed class NasaProvider(HttpClient httpClient) : IAggregationProvider
{
    public AggregationSource Source => AggregationSource.Nasa;

    public ContentCategory Category => ContentCategory.Media;

    public async Task<IReadOnlyList<AggregatedItem>> SearchAsync(
        ProviderSearchRequest request,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildRequestUri(request);
        using var response = await httpClient.GetAsync(
            requestUri,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var nasaResponse = await response.Content
            .ReadFromJsonAsync<NasaSearchResponse>(
                cancellationToken: cancellationToken);

        if (nasaResponse is null)
            throw new InvalidOperationException("NASA returned an empty response");

        return nasaResponse.Collection.Items
            .SelectMany(item => item.Data
                .Take(1)
                .Select(MapToAggregatedItem))
            .ToArray();
    }

    private static string BuildRequestUri(ProviderSearchRequest request)
    {
        var encodedQuery = Uri.EscapeDataString(request.Query.Trim());
        var requestUri =
            $"search?q={encodedQuery}" +
            $"&page_size={request.ResultsLimit}";

        if (request.FromDate is { } fromDate)
        {
            requestUri += $"&year_start={FormatYear(fromDate)}";
        }

        if (request.ToDate is { } toDate)
        {
            requestUri += $"&year_end={FormatYear(toDate)}";
        }

        return requestUri;
    }

    private static string FormatYear(DateOnly date)
        => date.Year.ToString(CultureInfo.InvariantCulture);

    private static AggregatedItem MapToAggregatedItem(NasaMediaDto media)
    {
        return new AggregatedItem
        {
            Id = $"nasa:{media.NasaId}",
            Source = AggregationSource.Nasa,
            Category = ContentCategory.Media,
            Title = media.Title,
            Description = media.Description,
            Url = $"https://images.nasa.gov/details/" +
                  Uri.EscapeDataString(media.NasaId),
            Timestamp = media.DateCreated
        };
    }
}
