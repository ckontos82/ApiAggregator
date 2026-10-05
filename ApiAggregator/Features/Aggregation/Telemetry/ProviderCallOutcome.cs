namespace ApiAggregator.Features.Aggregation.Telemetry;

/// <summary>
/// How one live provider call ended. Becomes the low-cardinality
/// <c>aggregator.outcome</c> tag on the duration histogram.
/// </summary>
internal enum ProviderCallOutcome
{
    /// <summary>The call completed without error (possibly with zero items).</summary>
    Success,

    /// <summary>The call was cancelled by the provider's timeout.</summary>
    Timeout,

    /// <summary>The provider responded with a failing HTTP status or the request failed in transit.</summary>
    HttpError,

    /// <summary>Any other failure (for example an unreadable response).</summary>
    Error
}
