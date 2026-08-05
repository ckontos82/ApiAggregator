using ApiAggregator.Features.Aggregation.Caching;
using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers;
using ApiAggregator.Features.Aggregation.Providers.GitHub;
using ApiAggregator.Features.Aggregation.Providers.Nasa;
using ApiAggregator.Features.Aggregation.Providers.NewsApi;
using ApiAggregator.Features.Aggregation.Services;
using ApiAggregator.Features.Aggregation.Statistics;
using System.Net.Http.Headers;

namespace ApiAggregator.Features.Aggregation;

/// <summary>
/// Registers everything the aggregation feature needs. Adding a new external
/// API means implementing <see cref="Providers.IAggregationProvider"/> and
/// registering it here (typed HttpClient + interface mapping).
/// </summary>
public static class AggregationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the aggregation providers, caching, statistics, and services.
    /// A provider whose configuration is missing (e.g. the NewsAPI key) is
    /// registered as a <see cref="Models.DisabledProvider"/> instead of
    /// failing startup.
    /// </summary>
    public static IServiceCollection AddAggregation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddGitHubProvider(services);
        AddNasaProvider(services);
        AddNewsApiProvider(services, configuration);

        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProviderCache, ProviderMemoryCache>();
        services.AddSingleton<IProviderStatisticsCollector, ProviderStatisticsCollector>();

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

        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<GitHubProvider>());
    }

    private static void AddNasaProvider(IServiceCollection services)
    {
        services.AddHttpClient<NasaProvider>(client =>
        {
            client.BaseAddress = new Uri("https://images-api.nasa.gov/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<NasaProvider>());
    }

    private static void AddNewsApiProvider(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var apiKey = configuration["ExternalApis:NewsApi:ApiKey"];

        // A missing key disables the NewsAPI provider instead of failing
        // startup; requests targeting it are rejected with a validation error.
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton(new DisabledProvider(
                AggregationSource.NewsApi,
                ContentCategory.Article,
                "NewsApi requires an API key: set the " +
                "'ExternalApis:NewsApi:ApiKey' configuration value to enable it."));

            return;
        }

        services.AddHttpClient<NewsApiProvider>(client =>
        {
            client.BaseAddress = new Uri("https://newsapi.org/v2/");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<NewsApiProvider>());
    }
}
