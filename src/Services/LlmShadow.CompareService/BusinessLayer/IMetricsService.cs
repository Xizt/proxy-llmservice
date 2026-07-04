using LlmShadow.Models.Response;

namespace LlmShadow.CompareService.BusinessLayer;

/// <summary>Provides real-time observability metrics for the shadow LLM comparison system.</summary>
public interface IMetricsService
{
    /// <summary>
    /// Returns the current metrics summary. Results are cached for a short TTL to avoid
    /// expensive aggregate queries on every request.
    /// </summary>
    /// <param name="cancellationToken">Token used to abort the operation.</param>
    /// <returns>A <see cref="MetricsSummaryDto"/> with total counts, error rates, and exact-match rate.</returns>
    Task<MetricsSummaryDto> GetMetricsAsync(CancellationToken cancellationToken = default);
}
