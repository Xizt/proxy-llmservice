namespace LlmShadow.DataLayer.Models;

/// <summary>
/// Stores the response received from the candidate (shadow) LLM for a given request.
/// Maps to the <c>SecondaryLlmResponses</c> table.
/// </summary>
public sealed class SecondaryLlmResponse : BaseEntity
{
    /// <summary>Gets or sets the application-level request identifier (FK to <c>Requests.RequestId</c>).</summary>
    public Guid RequestId { get; set; }

    /// <summary>Gets or sets the full response text. Null when <see cref="IsError"/> is <c>true</c>.</summary>
    public string? ResponseText { get; set; }

    /// <summary>Gets or sets a value indicating whether the secondary LLM call returned an error.</summary>
    public bool IsError { get; set; }

    /// <summary>Gets or sets the error message when <see cref="IsError"/> is <c>true</c>.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the latency of the secondary LLM call in milliseconds.</summary>
    public long LatencyMs { get; set; }

    /// <summary>Gets or sets the parent request record navigation property.</summary>
    public RequestRecord? Request { get; set; }
}
