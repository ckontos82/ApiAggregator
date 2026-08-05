namespace ApiAggregator.Features.Aggregation;

/// <summary>
/// Thrown when an aggregation query is impossible to fulfill as stated:
/// e.g. it names a source that is not available on this server, or a
/// source/category combination that matches no provider. Translated to a
/// 400 response by the controller.
/// </summary>
public sealed class InvalidAggregationRequestException(string message)
    : Exception(message);
