using ApiAggregator.Features.Aggregation.DTOs;
using ApiAggregator.Features.Aggregation.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Features.Aggregation.Controllers
{
    [ApiController]
    [Route("api/aggregation")]
    public class AggregationController(IAggregationService aggregationService) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<AggregationResponseDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AggregationResponseDto>> GetAsync(
            [FromQuery] AggregationQueryDto request,
            CancellationToken cancellationToken)
        {
            var response = await aggregationService.AggregateAsync(
                request,
                cancellationToken);

            return Ok(response);
        }
    }
}
