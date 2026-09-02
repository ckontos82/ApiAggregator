using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace ApiAggregator.Infrastructure.Logging;

/// <summary>
/// Adds the W3C trace context to every log event.
/// </summary>
/// <remarks>
/// Without this, the <c>traceId</c> that ProblemDetails returns to the
/// caller cannot be found in the logs: that value comes from
/// <see cref="Activity"/>, while Serilog's <c>RequestId</c> is
/// <c>HttpContext.TraceIdentifier</c> - a different identifier for the
/// same request. A caller quoting the trace id from an error response is
/// the most common way a support ticket starts, so it has to be queryable.
/// </remarks>
public sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var activity = Activity.Current;

        if (activity is null)
            return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}
