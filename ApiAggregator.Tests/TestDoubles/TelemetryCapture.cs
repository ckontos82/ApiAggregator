using ApiAggregator.Features.Aggregation.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ApiAggregator.Tests.TestDoubles;

/// <summary>
/// Captures the spans and metrics emitted by an <see cref="AggregationTelemetry"/>
/// instance. Create it in the test method body (<c>using var capture = new TelemetryCapture();</c>)
/// so <see cref="Activity.Current"/> flows into the code under test.
/// </summary>
/// <remarks>
/// The <see cref="ActivitySource"/> is static and xUnit runs test classes in
/// parallel, so two measures keep a capture to its own test. The listener's
/// sampler only creates activities that belong to this capture's trace, so
/// <c>StartActivity</c> returns <c>null</c> for every other test. Sampling
/// alone is not enough, though: an activity created for another capture is
/// still reported to every subscribed listener, so <c>ActivityStopped</c>
/// also filters by trace id. Metrics come from a meter factory per capture,
/// so captures never see each other's measurements.
/// </remarks>
internal sealed class TelemetryCapture : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];
    private readonly Lock _gate = new();

    public TelemetryCapture()
    {
        _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = _services.GetRequiredService<IMeterFactory>();

        Telemetry = new AggregationTelemetry(meterFactory);
        Durations = new MetricCollector<double>(
            meterFactory, AggregationTelemetry.Name, "aggregator.provider.duration");
        CacheLookups = new MetricCollector<long>(
            meterFactory, AggregationTelemetry.Name, "aggregator.cache.lookups");
        Results = new MetricCollector<long>(
            meterFactory, AggregationTelemetry.Name, "aggregator.provider.results");

        Parent = new Activity("test").Start();

        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AggregationTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                options.Parent.TraceId == Parent.TraceId
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.None,
            // Sample only decides whether an activity is created. Every listener
            // is still notified of every activity from the source, including
            // ones another capture sampled, so filter by trace here as well.
            ActivityStopped = activity =>
            {
                if (activity.TraceId != Parent.TraceId)
                {
                    return;
                }

                lock (_gate)
                {
                    _activities.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public AggregationTelemetry Telemetry { get; }

    public MetricCollector<double> Durations { get; }

    public MetricCollector<long> CacheLookups { get; }

    public MetricCollector<long> Results { get; }

    /// <summary>The ambient activity every captured span is parented to.</summary>
    public Activity Parent { get; }

    /// <summary>Snapshot of the activities stopped so far.</summary>
    public IReadOnlyList<Activity> Activities
    {
        get
        {
            lock (_gate)
            {
                return [.. _activities];
            }
        }
    }

    public void Dispose()
    {
        Parent.Stop();
        _listener.Dispose();
        Durations.Dispose();
        CacheLookups.Dispose();
        Results.Dispose();
        _services.Dispose();
    }
}
