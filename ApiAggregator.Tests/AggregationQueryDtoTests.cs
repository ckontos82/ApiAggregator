using ApiAggregator.Features.Aggregation.DTOs;
using System.ComponentModel.DataAnnotations;

namespace ApiAggregator.Tests
{
    public sealed class AggregationQueryDtoTests
    {
        [Fact]
        public void Validate_FromDateAfterToDate_ReturnsError()
        {
            var query = new AggregationQueryDto
            {
                Query = "apollo",
                FromDate = new DateOnly(2026, 8, 2),
                ToDate = new DateOnly(2026, 8, 1)
            };

            var results = query.Validate(new ValidationContext(query));

            var result = Assert.Single(results);
            Assert.Contains(nameof(AggregationQueryDto.FromDate), result.MemberNames);
            Assert.Contains(nameof(AggregationQueryDto.ToDate), result.MemberNames);
        }

        [Fact]
        public void Validate_ValidDateRange_ReturnsNoErrors()
        {
            var query = new AggregationQueryDto
            {
                Query = "apollo",
                FromDate = new DateOnly(2026, 8, 1),
                ToDate = new DateOnly(2026, 8, 1)
            };

            Assert.Empty(query.Validate(new ValidationContext(query)));
        }

        [Fact]
        public void Validate_MissingDates_ReturnsNoErrors()
        {
            var query = new AggregationQueryDto { Query = "apollo" };

            Assert.Empty(query.Validate(new ValidationContext(query)));
        }
    }
}
