namespace LlmShadow.DataLayer.Models;

/// <summary>
/// Stores the streamed response received from the primary LLM for a given request.
/// Maps to the <c>PrimaryLlmResponses</c> table.
/// </summary>
public sealed class PrimaryLlmResponse : BaseEntity
{
    /// <summary>Gets or sets the application-level request identifier (FK to <c>Requests.RequestId</c>).</summary>
    public Guid RequestId { get; set; }

    /// <summary>Gets or sets the full buffered text of the streamed response. Null when <see cref="IsError"/> is <c>true</c>.</summary>
    public string? ResponseText { get; set; }

    /// <summary>Gets or sets a value indicating whether the primary LLM call returned an error.</summary>
    public bool IsError { get; set; }

    /// <summary>Gets or sets the error message when <see cref="IsError"/> is <c>true</c>.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the total wall-clock latency in milliseconds for the primary call (including streaming).</summary>
    public long LatencyMs { get; set; }

    /// <summary>Gets or sets the parent request record navigation property.</summary>
    public RequestRecord? Request { get; set; }
}
