using ApiAggregator.Features.Aggregation.Providers;
using ApiAggregator.Features.Aggregation.Providers.GitHub;
using ApiAggregator.Features.Aggregation.Providers.NewsApi;
using ApiAggregator.Features.Aggregation.Services;
using Scalar.AspNetCore;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<GitHubProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
    client.Timeout = TimeSpan.FromSeconds(15);
});

var newsApiKey =
    builder.Configuration["ExternalApis:NewsApi:ApiKey"]
    ?? throw new InvalidOperationException(
        "NewsAPI API key is not configured.");

builder.Services.AddHttpClient<NewsApiProvider>(client =>
{
    client.BaseAddress = new Uri("https://newsapi.org/v2/");
    client.DefaultRequestHeaders.Add("X-Api-Key", newsApiKey);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<IAggregationProvider>(
    serviceProvider => serviceProvider.GetRequiredService<GitHubProvider>());

builder.Services.AddScoped<IAggregationProvider>(
    serviceProvider => serviceProvider.GetRequiredService<NewsApiProvider>());

builder.Services.AddScoped<IAggregationService, AggregationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(
        options => options.WithTitle("API Aggregator"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
