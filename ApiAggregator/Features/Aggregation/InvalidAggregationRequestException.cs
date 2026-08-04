namespace ApiAggregator.Features.Aggregation;

public sealed class InvalidAggregationRequestException(string message)
    : Exception(message);
