using Serilog.Core;
using Serilog.Events;

namespace ApiAggregator.Tests.TestDoubles;

/// <summary>Serilog sink that keeps every event it receives, for assertions.</summary>
internal sealed class CollectingSink : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
