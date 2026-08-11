using ApiAggregator.Features.Aggregation;
using ApiAggregator.Infrastructure.Logging;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json.Serialization;

// Logs anything that fails before the host is built.
SerilogRegistration.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replaces the default logging providers with Serilog.
    builder.AddSerilogLogging();

    // Add services to the container.

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter());
        });
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    builder.Services.AddProblemDetails();

    builder.Services.AddAggregation(builder.Configuration);

    var app = builder.Build();

    // Configure the HTTP request pipeline.

    // One summary line per request, in place of the framework's several.
    // Early in the pipeline so it also covers what the later middleware does.
    app.UseRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(
            options => options.WithTitle("API Aggregator"));
    }
    else
    {
        // Turns unhandled exceptions into RFC 7807 responses. In development
        // the developer exception page (with stack traces) is used instead.
        app.UseExceptionHandler();
    }

    // Gives empty error status codes (404, 405, ...) a ProblemDetails body.
    app.UseStatusCodePages();

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    Log.Information(
        "Starting ApiAggregator in {Environment}",
        app.Environment.EnvironmentName);

    app.Run();

    return 0;
}
catch (Exception exception)
{
    Log.Fatal(exception, "ApiAggregator terminated unexpectedly.");
    return 1;
}
finally
{
    // The SQL sink batches: without this, entries queued at shutdown are lost.
    Log.CloseAndFlush();
}
