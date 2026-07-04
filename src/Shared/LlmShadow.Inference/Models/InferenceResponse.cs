namespace LlmShadow.Inference.Models;

/// <summary>The aggregated result from a non-streaming DO Inference call.</summary>
public sealed record InferenceResponse
{
    /// <summary>Gets the full text content produced by the model.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Gets the model that produced the response, as reported by the API.</summary>
    public string Model { get; init; } = string.Empty;
}
