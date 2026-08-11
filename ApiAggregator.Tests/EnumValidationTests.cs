using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Enums;
using System.ComponentModel.DataAnnotations;

namespace ApiAggregator.Tests
{
    public sealed class AggregationQueryDtoEnumTests
    {
        [Fact]
        public void Validate_UnknownSortField_ReturnsError()
        {
            var query = new AggregationQueryDto
            {
                Query = "apollo",
                SortBy = (AggregationSortField)99
            };

            var result = Assert.Single(query.Validate(new ValidationContext(query)));

            Assert.Contains(nameof(AggregationQueryDto.SortBy), result.MemberNames);
        }

        [Fact]
        public void Validate_UnknownSortDirection_ReturnsError()
        {
            var query = new AggregationQueryDto
            {
                Query = "apollo",
                SortDirection = (SortDirection)99
            };

            var result = Assert.Single(query.Validate(new ValidationContext(query)));

            Assert.Contains(nameof(AggregationQueryDto.SortDirection), result.MemberNames);
        }

        [Fact]
        public void Validate_UnknownCategory_ReturnsError()
        {
            var query = new AggregationQueryDto
            {
                Query = "apollo",
                Category = (ContentCategory)99
            };

            var result = Assert.Single(query.Validate(new ValidationContext(query)));

            Assert.Contains(nameof(AggregationQueryDto.Category), result.MemberNames);
        }

        [Fact]
        public void Validate_UnknownSourceAmongValidOnes_ReturnsError()
        {
            var query = new AggregationQueryDto
            {
                Query = "apollo",
                Sources = [AggregationSource.GitHub, (AggregationSource)99]
            };

            var result = Assert.Single(query.Validate(new ValidationContext(query)));

            Assert.Contains(nameof(AggregationQueryDto.Sources), result.MemberNames);
        }

        [Fact]
        public void Validate_AllEnumsValid_ReturnsNoErrors()
        {
            var query = new AggregationQueryDto
            {
                Query = "apollo",
                SortBy = AggregationSortField.Title,
                SortDirection = SortDirection.Ascending,
                Category = ContentCategory.Repository,
                Sources = [AggregationSource.GitHub]
            };

            Assert.Empty(query.Validate(new ValidationContext(query)));
        }

        [Fact]
        public void Validate_Defaults_ReturnNoErrors()
        {
            var query = new AggregationQueryDto { Query = "apollo" };

            Assert.Empty(query.Validate(new ValidationContext(query)));
        }
    }
}
