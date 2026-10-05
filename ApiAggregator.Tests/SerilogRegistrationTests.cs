using ApiAggregator.Infrastructure.Logging;
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

    private static LogEvent CreateEvent(string? path)
    {
        var properties = new List<LogEventProperty>();

        if (path is not null)
        {
            properties.Add(new LogEventProperty("RequestPath", new ScalarValue(path)));
        }

        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplateParser().Parse(""),
            properties);
    }
}
