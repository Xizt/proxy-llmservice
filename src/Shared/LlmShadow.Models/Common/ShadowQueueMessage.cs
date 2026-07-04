namespace LlmShadow.Models.Common;

/// <summary>
/// Message payload placed on the Redis Stream by the ProxyService and consumed by the SecondaryProcessor.
/// All fields are serialised as Redis hash entries.
/// </summary>
public sealed record ShadowQueueMessage
{
    /// <summary>Gets the unique identifier linking this shadow message to its <c>RequestRecord</c> row.</summary>
    public Guid RequestId { get; init; }

    /// <summary>Gets the original JSON-serialised <c>ChatRequestDto</c> forwarded to the candidate LLM.</summary>
    public string RequestPayloadJson { get; init; } = string.Empty;

    /// <summary>Gets the candidate model identifier to invoke.</summary>
    public string CandidateModel { get; init; } = string.Empty;

    /// <summary>Gets the number of previous delivery attempts, used for dead-letter threshold enforcement.</summary>
    public int RetryCount { get; init; } = 0;
}
