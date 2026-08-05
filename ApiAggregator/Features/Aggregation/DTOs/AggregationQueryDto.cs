using ApiAggregator.Features.Aggregation.Enums;
using System.ComponentModel.DataAnnotations;

namespace ApiAggregator.Features.Aggregation.DTOs
{
    /// <summary>
    /// Query parameters accepted by the aggregation endpoint.
    /// </summary>
    public sealed class AggregationQueryDto : IValidatableObject
    {
        /// <summary>Search term sent to every selected provider.</summary>
        [Required]
        [StringLength(100)]
        public string Query { get; init; } = string.Empty;

        /// <summary>Restricts the search to these sources; omit to query all available providers. Repeat the parameter for multiple values.</summary>
        public AggregationSource[]? Sources { get; init; }

        /// <summary>Restricts the search to providers of this content category.</summary>
        public ContentCategory? Category { get; init; }

        /// <summary>Earliest item date to include (inclusive).</summary>
        public DateOnly? FromDate { get; init; }

        /// <summary>Latest item date to include (inclusive).</summary>
        public DateOnly? ToDate { get; init; }

        /// <summary>Field the merged results are sorted by.</summary>
        public AggregationSortField SortBy { get; init; } = AggregationSortField.Timestamp;

        /// <summary>Sort direction for the merged results.</summary>
        public SortDirection SortDirection { get; init; } = SortDirection.Descending;

        /// <summary>Maximum number of items requested from each provider.</summary>
        [Range(1, 25)]
        public int ResultsPerSource { get; init; } = 10;

        /// <summary>Validates rules that span multiple properties.</summary>
        public IEnumerable<ValidationResult> Validate(
                ValidationContext validationContext
            )
        {
            if (FromDate > ToDate)
            {
                yield return new ValidationResult(
                    "FromDate cannot be later than ToDate",
                    new[] { nameof(FromDate), nameof(ToDate) });
            }
        }
    }
}
