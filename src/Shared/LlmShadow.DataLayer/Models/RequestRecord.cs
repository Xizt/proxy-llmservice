using LlmShadow.Models.Common;

namespace LlmShadow.DataLayer.Models;

/// <summary>
/// Represents a single proxied chat request and its full lifecycle state.
/// Maps to the <c>Requests</c> table.
/// </summary>
public sealed class RequestRecord : BaseEntity
{
    /// <summary>Gets or sets the application-level unique identifier for this request.</summary>
    public Guid RequestId { get; set; }

    /// <summary>Gets or sets the current lifecycle status of the request.</summary>
    public RequestStatus Status { get; set; }

    /// <summary>Gets or sets the primary model identifier that served the hot-path response.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Gets or sets the candidate model identifier used for shadow evaluation.</summary>
    public string CandidateModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-serialised original <c>ChatRequestDto</c> payload.</summary>
    public string RequestPayloadJson { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the primary and candidate <c>action</c> values matched. Null until evaluated.</summary>
    public bool? IsMatch { get; set; }

    /// <summary>Gets or sets the match percentage (100.0 or 0.0) assigned by the evaluator. Null until evaluated.</summary>
    public double? MatchPercentage { get; set; }

    /// <summary>Gets or sets the UTC timestamp when evaluation was completed. Null until evaluated.</summary>
    public DateTime? EvaluatedAtUtc { get; set; }

    /// <summary>Gets or sets the associated primary LLM response (one-to-one).</summary>
    public PrimaryLlmResponse? PrimaryResponse { get; set; }

    /// <summary>Gets or sets the associated secondary LLM response (one-to-one).</summary>
    public SecondaryLlmResponse? SecondaryResponse { get; set; }
}
