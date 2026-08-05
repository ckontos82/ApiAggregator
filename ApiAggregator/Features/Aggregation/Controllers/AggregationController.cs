using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Services;
using ApiAggregator.Features.Aggregation.Statistics;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Features.Aggregation.Controllers
{
    /// <summary>
    /// Aggregated search across the configured external APIs.
    /// </summary>
    [ApiController]
    [Route("api/aggregation")]
    public class AggregationController(
        IAggregationService aggregationService,
        IProviderStatisticsCollector statisticsCollector) : ControllerBase
    {
        /// <summary>
        /// Searches all requested external APIs in parallel and returns the
        /// merged, filtered, and sorted results.
        /// </summary>
        /// <remarks>
        /// Providers that fail at request time degrade gracefully: the
        /// response is still 200 and the failure is reported per provider.
        /// Requesting a source that is not available on the server, or a
        /// source/category combination that matches no provider, returns 400.
        /// </remarks>
        /// <param name="request">Search term plus optional source, category, date, sorting, and paging options.</param>
        /// <param name="cancellationToken">Cancels the in-flight provider calls when the client disconnects.</param>
        /// <returns>The aggregated items together with the execution status of each provider.</returns>
        [HttpGet]
        [ProducesResponseType<AggregationResponseDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AggregationResponseDto>> GetAsync(
            [FromQuery] AggregationQueryDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await aggregationService.AggregateAsync(
                    request,
                    cancellationToken);

                return Ok(response);
            }
            catch (InvalidAggregationRequestException exception)
            {
                return ValidationProblem(detail: exception.Message);
            }
        }

        /// <summary>
        /// Returns in-memory request statistics for each external API.
        /// </summary>
        /// <remarks>
        /// Counts only real external calls; cache hits are excluded. Response
        /// times are grouped into performance buckets: fast (&lt;100 ms),
        /// average (100-200 ms), and slow (&gt;200 ms). Statistics reset when
        /// the application restarts.
        /// </remarks>
        [HttpGet("statistics")]
        [ProducesResponseType<IReadOnlyList<ProviderStatisticsDto>>(StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<ProviderStatisticsDto>> GetStatistics()
        {
            return Ok(statisticsCollector.GetSnapshot());
        }
    }
}
