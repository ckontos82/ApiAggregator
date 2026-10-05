using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ApiAggregator.Features.Aggregation.Telemetry;

/// <summary>
/// The aggregation feature's custom telemetry: one span per provider call
/// plus three metrics (call duration, cache lookups, provider results).
/// </summary>
/// <remarks>
/// The <see cref="ActivitySource"/> is static, which is the standard
/// practice for tracing (it has no DI-scoped state and costs nothing without
/// a listener). The <see cref="Meter"/> is created from <see cref="IMeterFactory"/>
/// instead, so it is scoped to the DI container and tests can observe it in
/// isolation. Tags are low-cardinality only: the search query is never a tag.
/// </remarks>
internal sealed class AggregationTelemetry
{
    /// <summary>Name of the activity source and the meter.</summary>
    public const string Name = "ApiAggregator";

    internal const string DurationInstrument = "aggregator.provider.duration";
    internal const string CacheLookupsInstrument = "aggregator.cache.lookups";
    internal const string ResultsInstrument = "aggregator.provider.results";

    private const string ProviderOperationName = "aggregation.provider";

    private const string SourceTag = "aggregator.source";
    private const string CacheHitTag = "aggregator.cache.hit";
    private const string CacheResultTag = "aggregator.cache.result";
    private const string ProviderStatusTag = "aggregator.provider.status";
    private const string ItemCountTag = "aggregator.item_count";
    private const string OutcomeTag = "aggregator.outcome";

    private static readonly ActivitySource ActivitySource = new(Name);

    private readonly Histogram<double> _providerDuration;
    private readonly Counter<long> _cacheLookups;
    private readonly Counter<long> _providerResults;

    public AggregationTelemetry(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(Name);

        _providerDuration = meter.CreateHistogram<double>(
            DurationInstrument,
            unit: "s",
            description: "Duration of live provider calls.",
            tags: null,
            // The OpenTelemetry SDK's default bucket bounds (0, 5, 10, 25 ...)
            // are sized for milliseconds and only swapped for second-sized
            // ones on a hard-coded list of well-known instruments. Without
            // this advice every provider call falls into the first bucket and
            // the dashboard percentiles are meaningless. The bounds extend past
            // the default provider timeout (ProviderHttpOptions.TimeoutSeconds,
            // 15 s) so timed-out calls land in a finite bucket; raise them if a
            // longer timeout is configured.
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 15, 20]
            });
        _cacheLookups = meter.CreateCounter<long>(
            CacheLookupsInstrument,
            unit: "{lookup}",
            description: "Provider cache lookups, by hit or miss.");
        _providerResults = meter.CreateCounter<long>(
            ResultsInstrument,
            unit: "{result}",
            description: "Provider results, by final status.");
    }

    /// <summary>
    /// Starts the span for one provider call. Returns <c>null</c> when nothing
    /// is listening, so callers must treat the activity as optional.
    /// </summary>
    public Activity? StartProviderActivity(AggregationSource source)
    {
        var activity = ActivitySource.StartActivity(ProviderOperationName, ActivityKind.Internal);

        if (activity is not null)
        {
            activity.DisplayName = $"{source} provider";
            activity.SetTag(SourceTag, source.ToString());
        }

        return activity;
    }

    /// <summary>Counts a cache lookup and marks the span with whether it hit.</summary>
    public void RecordCacheLookup(Activity? activity, AggregationSource source, bool hit)
    {
        activity?.SetTag(CacheHitTag, hit);

        _cacheLookups.Add(1, new TagList
        {
            { SourceTag, source.ToString() },
            { CacheResultTag, hit ? "hit" : "miss" }
        });
    }

    /// <summary>Records how long a live provider call took, in seconds.</summary>
    public void RecordProviderDuration(
        AggregationSource source,
        TimeSpan elapsed,
        ProviderCallOutcome outcome)
    {
        _providerDuration.Record(elapsed.TotalSeconds, new TagList
        {
            { SourceTag, source.ToString() },
            { OutcomeTag, ToTagValue(outcome) }
        });
    }

    /// <summary>
    /// Tags the span with the final status and item count, marks it as an
    /// error when the result is degraded or unavailable (the error message
    /// becomes the status description), and counts the result.
    /// </summary>
    public void RecordProviderResult(Activity? activity, ProviderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var status = result.Status.ToString();

        if (activity is not null)
        {
            activity.SetTag(ProviderStatusTag, status);
            activity.SetTag(ItemCountTag, result.Items.Count);

            // Keyed on status, not on the message: Degraded and Unavailable
            // are errors even if a provider forgets to set a message.
            if (result.Status is not ProviderStatus.Succeeded)
            {
                activity.SetStatus(ActivityStatusCode.Error, result.ErrorMessage);
            }
        }

        _providerResults.Add(1, new TagList
        {
            { SourceTag, result.Source.ToString() },
            { ProviderStatusTag, status }
        });
    }

    private static string ToTagValue(ProviderCallOutcome outcome) => outcome switch
    {
        ProviderCallOutcome.Success => "success",
        ProviderCallOutcome.Timeout => "timeout",
        ProviderCallOutcome.HttpError => "http_error",
        ProviderCallOutcome.Error => "error",
        // Never throw here: this also runs inside the service's catch blocks,
        // where a throw would turn a degraded 200 response into a 500.
        _ => "error"
    };
}
