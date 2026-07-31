using ApiAggregator.Features.Aggregation.Enums;
using System.ComponentModel.DataAnnotations;

namespace ApiAggregator.Features.Aggregation.DTOs
{
    public sealed class AggregationQueryDto : IValidatableObject
    {
        [Required]
        [StringLength(100)]
        public string Query { get; init; } = string.Empty;
        public AggregationSource[]? Sources { get; init; }
        public ContentCategory? Category { get; init; }
        public DateOnly? FromDate { get; init; }
        public DateOnly? ToDate { get; init; }
        public AggregationSortField SortBy { get; init; } = AggregationSortField.Timestamp;
        public SortDirection SortDirection { get; init; } = SortDirection.Descending;

        [Range(1, 25)]
        public int ResultsPerSource { get; init; } = 10;

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
