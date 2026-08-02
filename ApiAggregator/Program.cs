using ApiAggregator.Features.Aggregation.Providers;
using ApiAggregator.Features.Aggregation.Providers.GitHub;
using Scalar.AspNetCore;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<GitHubProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");

    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "ApiAggregator/1.0");

    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue(
            "application/vnd.github+json"));

    client.DefaultRequestHeaders.Add(
        "X-GitHub-Api-Version",
        "2026-03-10");
});

builder.Services.AddScoped<IAggregationProvider>(
    serviceProvider =>
        serviceProvider.GetRequiredService<GitHubProvider>());

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
