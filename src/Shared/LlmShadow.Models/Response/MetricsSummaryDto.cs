namespace LlmShadow.Models.Response;

/// <summary>Real-time observability summary returned by <c>GET /metrics</c>.</summary>
public sealed record MetricsSummaryDto
{
    /// <summary>Gets the total number of proxied requests stored in the system.</summary>
    public long TotalRequests { get; init; }

    /// <summary>Gets the count of shadow executions that ended with <c>SecondaryLLMResponseFailed</c>.</summary>
    public long ShadowErrors { get; init; }

    /// <summary>Gets the count of shadow executions that exceeded their timeout.</summary>
    public long ShadowTimeouts { get; init; }

    /// <summary>
    /// Gets the percentage of evaluated requests where primary and candidate <c>action</c> keys matched exactly.
    /// Calculated as <c>Matched / (Matched + Failed) * 100</c>; returns 0 when no requests have been evaluated.
    /// </summary>
    public double ExactMatchRatePercent { get; init; }

    /// <summary>Gets the count of requests that have been evaluated (matched + failed).</summary>
    public long EvaluatedRequests { get; init; }

    /// <summary>Gets a per-status breakdown of request counts keyed by status name.</summary>
    public IReadOnlyDictionary<string, long> StatusBreakdown { get; init; } =
        new Dictionary<string, long>();
}
