using ApiAggregator.Features.Aggregation.Enums;
using ApiAggregator.Features.Aggregation.Models;
using ApiAggregator.Features.Aggregation.Telemetry;
using ApiAggregator.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ApiAggregator.Tests;

public sealed class AggregationTelemetryTests
{
    [Fact]
    public void StartProviderActivity_SetsNameDisplayNameAndSourceTag()
    {
        using var capture = new TelemetryCapture();

        capture.Telemetry.StartProviderActivity(AggregationSource.GitHub)?.Dispose();

        var activity = Assert.Single(capture.Activities);
        Assert.Equal("aggregation.provider", activity.OperationName);
        Assert.Equal("GitHub provider", activity.DisplayName);
        Assert.Equal("GitHub", activity.GetTagItem("aggregator.source"));
        Assert.Equal(capture.Parent.SpanId, activity.ParentSpanId);
    }

    [Fact]
    public void StartProviderActivity_WithoutListener_ReturnsNull()
    {
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var telemetry = new AggregationTelemetry(services.GetRequiredService<IMeterFactory>());

        var activity = telemetry.StartProviderActivity(AggregationSource.Nasa);

        Assert.Null(activity);
        telemetry.RecordCacheLookup(null, AggregationSource.Nasa, hit: true);
        AggregationTelemetry.RecordException(null, new InvalidOperationException());
        telemetry.RecordProviderResult(null, CreateResult(ProviderStatus.Succeeded));
    }

    [Fact]
    public void RecordCacheLookup_Hit_IncrementsCounterAndTagsSpan()
    {
        using var capture = new TelemetryCapture();
        using var activity = capture.Telemetry.StartProviderActivity(AggregationSource.Nasa);

        capture.Telemetry.RecordCacheLookup(activity, AggregationSource.Nasa, hit: true);

        var measurement = Assert.Single(capture.CacheLookups.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal("Nasa", measurement.Tags["aggregator.source"]);
        Assert.Equal("hit", measurement.Tags["aggregator.cache.result"]);
        Assert.Equal(true, activity!.GetTagItem("aggregator.cache.hit"));
    }

    [Fact]
    public void RecordCacheLookup_Miss_TagsMiss()
    {
        using var capture = new TelemetryCapture();
        using var activity = capture.Telemetry.StartProviderActivity(AggregationSource.Nasa);

        capture.Telemetry.RecordCacheLookup(activity, AggregationSource.Nasa, hit: false);

        var measurement = Assert.Single(capture.CacheLookups.GetMeasurementSnapshot());
        Assert.Equal("miss", measurement.Tags["aggregator.cache.result"]);
        Assert.Equal(false, activity!.GetTagItem("aggregator.cache.hit"));
    }

    [Theory]
    [InlineData(ProviderCallOutcome.Success, "success")]
    [InlineData(ProviderCallOutcome.Timeout, "timeout")]
    [InlineData(ProviderCallOutcome.HttpError, "http_error")]
    [InlineData(ProviderCallOutcome.Error, "error")]
    public void RecordProviderDuration_RecordsSecondsWithOutcome(
        object outcome, // ProviderCallOutcome is internal; xUnit theory methods must be public.
        string expectedTag)
    {
        using var capture = new TelemetryCapture();

        capture.Telemetry.RecordProviderDuration(
            AggregationSource.GitHub,
            TimeSpan.FromMilliseconds(150),
            (ProviderCallOutcome)outcome);

        var measurement = Assert.Single(capture.Durations.GetMeasurementSnapshot());
        Assert.Equal(0.15, measurement.Value, precision: 10);
        Assert.Equal("GitHub", measurement.Tags["aggregator.source"]);
        Assert.Equal(expectedTag, measurement.Tags["aggregator.outcome"]);
    }

    [Fact]
    public void RecordProviderDuration_UnknownOutcome_TagsErrorInsteadOfThrowing()
    {
        using var capture = new TelemetryCapture();

        capture.Telemetry.RecordProviderDuration(
            AggregationSource.GitHub,
            TimeSpan.FromMilliseconds(150),
            (ProviderCallOutcome)999);

        var measurement = Assert.Single(capture.Durations.GetMeasurementSnapshot());
        Assert.Equal("error", measurement.Tags["aggregator.outcome"]);
    }

    [Fact]
    public void ProviderDurationHistogram_UsesSecondSizedBucketBoundaries()
    {
        using var capture = new TelemetryCapture();
        capture.Telemetry.RecordProviderDuration(
            AggregationSource.GitHub,
            TimeSpan.FromMilliseconds(150),
            ProviderCallOutcome.Success);

        var histogram = Assert.IsType<Histogram<double>>(capture.Durations.Instrument);

        Assert.Equal(
            [0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 15, 20],
            histogram.Advice?.HistogramBucketBoundaries);
    }

    [Fact]
    public void RecordProviderResult_Succeeded_TagsSpanAndCounts()
    {
        using var capture = new TelemetryCapture();
        using var activity = capture.Telemetry.StartProviderActivity(AggregationSource.GitHub);

        capture.Telemetry.RecordProviderResult(
            activity, CreateResult(ProviderStatus.Succeeded, itemCount: 2));

        Assert.Equal("Succeeded", activity!.GetTagItem("aggregator.provider.status"));
        Assert.Equal(2, activity.GetTagItem("aggregator.item_count"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        var measurement = Assert.Single(capture.Results.GetMeasurementSnapshot());
        Assert.Equal("Succeeded", measurement.Tags["aggregator.provider.status"]);
    }

    [Theory]
    [InlineData(ProviderStatus.Degraded)]
    [InlineData(ProviderStatus.Unavailable)]
    public void RecordProviderResult_WithError_SetsErrorStatus(ProviderStatus status)
    {
        using var capture = new TelemetryCapture();
        using var activity = capture.Telemetry.StartProviderActivity(AggregationSource.GitHub);

        capture.Telemetry.RecordProviderResult(
            activity, CreateResult(status, errorMessage: "down"));

        Assert.Equal(ActivityStatusCode.Error, activity!.Status);
        Assert.Equal("down", activity.StatusDescription);
        var measurement = Assert.Single(capture.Results.GetMeasurementSnapshot());
        Assert.Equal(status.ToString(), measurement.Tags["aggregator.provider.status"]);
    }

    [Fact]
    public void RecordProviderResult_DegradedWithoutMessage_StillSetsErrorStatus()
    {
        using var capture = new TelemetryCapture();
        using var activity = capture.Telemetry.StartProviderActivity(AggregationSource.GitHub);

        capture.Telemetry.RecordProviderResult(
            activity, CreateResult(ProviderStatus.Degraded, errorMessage: null));

        Assert.Equal(ActivityStatusCode.Error, activity!.Status);
    }

    [Fact]
    public void RecordException_AddsExceptionEvent()
    {
        using var capture = new TelemetryCapture();
        using var activity = capture.Telemetry.StartProviderActivity(AggregationSource.GitHub);

        AggregationTelemetry.RecordException(activity, new InvalidOperationException("boom"));

        var activityEvent = Assert.Single(activity!.Events);
        Assert.Equal("exception", activityEvent.Name);
    }

    private static ProviderResult CreateResult(
        ProviderStatus status,
        int itemCount = 0,
        string? errorMessage = null)
        => new()
        {
            Source = AggregationSource.GitHub,
            Items = [.. Enumerable.Range(0, itemCount).Select(CreateItem)],
            Status = status,
            ErrorMessage = errorMessage
        };

    private static AggregatedItem CreateItem(int index)
        => new()
        {
            Id = $"github:{index}",
            Source = AggregationSource.GitHub,
            Category = ContentCategory.Repository,
            Title = $"Item {index}",
            Timestamp = DateTimeOffset.UnixEpoch
        };
}
