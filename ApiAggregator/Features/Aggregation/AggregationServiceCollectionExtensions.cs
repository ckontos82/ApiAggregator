using ApiAggregator.Features.Aggregation.Caching;
using ApiAggregator.Features.Aggregation.Configuration;
using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Providers;
using ApiAggregator.Features.Aggregation.Providers.GitHub;
using ApiAggregator.Features.Aggregation.Providers.Nasa;
using ApiAggregator.Features.Aggregation.Providers.NewsApi;
using ApiAggregator.Features.Aggregation.Services;
using ApiAggregator.Features.Aggregation.Statistics;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace ApiAggregator.Features.Aggregation;

/// <summary>
/// Registers everything the aggregation feature needs. Adding a new external
/// API means implementing <see cref="Providers.IAggregationProvider"/>, adding
/// its <c>ExternalApis:{name}</c> configuration section, and registering it
/// here (options + typed HttpClient + interface mapping).
/// </summary>
public static class AggregationServiceCollectionExtensions
{
    private const string GitHubOptionsName = "GitHub";
    private const string NasaOptionsName = "Nasa";
    private const string NewsApiOptionsName = "NewsApi";

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

        AddGitHubProvider(services, configuration);
        AddNasaProvider(services, configuration);
        AddNewsApiProvider(services, configuration);

        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProviderCache, ProviderMemoryCache>();
        services.AddSingleton<IProviderStatisticsCollector, ProviderStatisticsCollector>();

        services.AddScoped<IAggregationService, AggregationService>();

        return services;
    }

    /// <summary>
    /// Binds and validates one provider's HTTP settings as a named options
    /// instance. ValidateOnStart turns a bad configuration into a startup
    /// failure instead of a 500 on the first request.
    /// </summary>
    private static OptionsBuilder<ProviderHttpOptions> AddProviderOptions(
        IServiceCollection services,
        IConfiguration configuration,
        string name)
    {
        return services
            .AddOptions<ProviderHttpOptions>(name)
            .Bind(configuration.GetSection($"{ProviderHttpOptions.SectionName}:{name}"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static ProviderHttpOptions GetOptions(IServiceProvider serviceProvider, string name)
    {
        return serviceProvider
            .GetRequiredService<IOptionsMonitor<ProviderHttpOptions>>()
            .Get(name);
    }

    /// <summary>Settings every provider shares; provider-specific headers are added by the caller.</summary>
    private static void ApplyCommonSettings(HttpClient client, ProviderHttpOptions options)
    {
        client.BaseAddress = new Uri(options.BaseAddress);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    }

    private static void AddGitHubProvider(IServiceCollection services, IConfiguration configuration)
    {
        AddProviderOptions(services, configuration, GitHubOptionsName);

        services.AddHttpClient<GitHubProvider>((serviceProvider, client) =>
        {
            var options = GetOptions(serviceProvider, GitHubOptionsName);

            ApplyCommonSettings(client, options);

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            if (options.ApiVersion is { } apiVersion)
            {
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", apiVersion);
            }
        });

        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<GitHubProvider>());
    }

    private static void AddNasaProvider(IServiceCollection services, IConfiguration configuration)
    {
        AddProviderOptions(services, configuration, NasaOptionsName);

        services.AddHttpClient<NasaProvider>((serviceProvider, client) =>
            ApplyCommonSettings(client, GetOptions(serviceProvider, NasaOptionsName)));

        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<NasaProvider>());
    }

    private static void AddNewsApiProvider(IServiceCollection services, IConfiguration configuration)
    {
        // Read straight from configuration: options are resolved lazily, so
        // they are not available while the registrations are still being built.
        var apiKey = configuration[
            $"{ProviderHttpOptions.SectionName}:{NewsApiOptionsName}:ApiKey"];

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

        AddProviderOptions(services, configuration, NewsApiOptionsName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "ExternalApis:NewsApi:ApiKey must not be empty.");

        services.AddHttpClient<NewsApiProvider>((serviceProvider, client) =>
        {
            var options = GetOptions(serviceProvider, NewsApiOptionsName);

            ApplyCommonSettings(client, options);

            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        services.AddScoped<IAggregationProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<NewsApiProvider>());
    }
}
