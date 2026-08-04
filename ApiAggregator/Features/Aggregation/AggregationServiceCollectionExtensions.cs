using ApiAggregator.Features.Aggregation.Caching;
using ApiAggregator.Features.Aggregation.Providers;
using ApiAggregator.Features.Aggregation.Providers.GitHub;
using ApiAggregator.Features.Aggregation.Providers.Nasa;
using ApiAggregator.Features.Aggregation.Providers.NewsApi;
using ApiAggregator.Features.Aggregation.Services;
using System.Net.Http.Headers;

namespace ApiAggregator.Features.Aggregation;

public static class AggregationServiceCollectionExtensions
{
    public static IServiceCollection AddAggregation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddGitHubProvider(services);
        AddNasaProvider(services);
        AddNewsApiProvider(services, configuration);

        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<GitHubProvider>());
        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<NasaProvider>());
        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<NewsApiProvider>());

        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProviderCache, ProviderMemoryCache>();

        services.AddScoped<IAggregationService, AggregationService>();

        return services;
    }

    private static void AddGitHubProvider(IServiceCollection services)
    {
        services.AddHttpClient<GitHubProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
    }

    private static void AddNasaProvider(IServiceCollection services)
    {
        services.AddHttpClient<NasaProvider>(client =>
        {
            client.BaseAddress = new Uri("https://images-api.nasa.gov/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
    }

    private static void AddNewsApiProvider(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var apiKey = configuration["ExternalApis:NewsApi:ApiKey"]
            ?? throw new InvalidOperationException(
                "NewsAPI API key is not configured.");

        services.AddHttpClient<NewsApiProvider>(client =>
        {
            client.BaseAddress = new Uri("https://newsapi.org/v2/");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
    }
}
