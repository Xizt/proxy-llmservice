using LlmShadow.CompareService.BusinessLayer;
using LlmShadow.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace LlmShadow.CompareService.Controllers;

/// <summary>Exposes real-time observability metrics for the shadow LLM comparison system.</summary>
[ApiController]
[Route("[controller]")]
public sealed class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;

    /// <summary>Initialises the controller.</summary>
    public MetricsController(IMetricsService metricsService) =>
        _metricsService = metricsService;

    /// <summary>
    /// Returns a real-time summary including total request count, shadow error/timeout counts,
    /// and the exact-match rate percentage between primary and candidate model outputs.
    /// Results are served from an in-process cache with a short TTL to limit database load.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MetricsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MetricsSummaryDto>> GetMetrics(CancellationToken cancellationToken)
    {
        var summary = await _metricsService.GetMetricsAsync(cancellationToken);
        return Ok(summary);
    }
}
