using ApiAggregator.Infrastructure.Logging;
using ApiAggregator.Tests.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;

namespace ApiAggregator.Tests;

public sealed class SerilogRegistrationTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/HEALTH")]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    public void IsNonApiRequest_ReturnsTrue_ForInfrastructurePaths(string path)
        => Assert.True(SerilogRegistration.IsNonApiRequest(CreateEvent(path)));

    [Theory]
    [InlineData("/api/aggregation")]
    [InlineData("/api/aggregation/statistics")]
    public void IsNonApiRequest_ReturnsFalse_ForApiPaths(string path)
        => Assert.False(SerilogRegistration.IsNonApiRequest(CreateEvent(path)));

    [Fact]
    public void IsNonApiRequest_ReturnsFalse_WhenNoRequestPath()
        => Assert.False(SerilogRegistration.IsNonApiRequest(CreateEvent(path: null)));

    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/scalar")]
    public void IsNonApiRequest_ReturnsTrue_ForWholeSegmentMatches(string path)
        => Assert.True(SerilogRegistration.IsNonApiRequest(CreateEvent(path)));

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/alive2")]
    [InlineData("/scalarish")]
    public void IsNonApiRequest_ReturnsFalse_ForPartialSegmentMatches(string path)
        => Assert.False(SerilogRegistration.IsNonApiRequest(CreateEvent(path)));

    [Theory]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Information)]
    public void IsRoutineNonApiEvent_ReturnsTrue_BelowWarning(LogEventLevel level)
        => Assert.True(SerilogRegistration.IsRoutineNonApiEvent(CreateEvent("/health", level)));

    [Theory]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void IsRoutineNonApiEvent_ReturnsFalse_ForWarningsAndErrors(LogEventLevel level)
        => Assert.False(SerilogRegistration.IsRoutineNonApiEvent(CreateEvent("/health", level)));

    [Fact]
    public void IsRoutineNonApiEvent_ReturnsFalse_ForApiPaths()
        => Assert.False(SerilogRegistration.IsRoutineNonApiEvent(CreateEvent("/api/aggregation")));

    [Fact]
    public void Configure_DropsRoutineHealthEvents_KeepsHealthWarnings()
    {
        var sink = new CollectingSink();
        var loggerConfiguration = new LoggerConfiguration();

        // Empty configuration: no LogDatabase connection string, so no SQL sink.
        SerilogRegistration.Configure(
            loggerConfiguration,
            new ConfigurationBuilder().Build(),
            new HostingEnvironment { EnvironmentName = "Test" });

        using (var logger = loggerConfiguration.WriteTo.Sink(sink).CreateLogger())
        {
            var healthLogger = logger.ForContext("RequestPath", "/health");

            healthLogger.Information("Routine health check");
            healthLogger.Warning("Failing health check");
        }

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, logEvent.Level);
    }

    private static LogEvent CreateEvent(string? path, LogEventLevel level = LogEventLevel.Information)
    {
        var properties = new List<LogEventProperty>();

        if (path is not null)
        {
            properties.Add(new LogEventProperty("RequestPath", new ScalarValue(path)));
        }

        return new LogEvent(
            DateTimeOffset.Now,
            level,
            exception: null,
            new MessageTemplateParser().Parse(""),
            properties);
    }
}
