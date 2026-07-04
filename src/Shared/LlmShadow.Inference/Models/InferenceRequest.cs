namespace LlmShadow.Inference.Models;

/// <summary>Encapsulates all parameters for a single call to the DO Serverless Inference API.</summary>
public sealed record InferenceRequest
{
    /// <summary>Gets the model identifier (e.g. <c>meta-llama/Meta-Llama-3.1-8B-Instruct</c>).</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Gets the ordered list of conversation messages.</summary>
    public IReadOnlyList<InferenceChatMessage> Messages { get; init; } = Array.Empty<InferenceChatMessage>();

    /// <summary>Gets an optional sampling temperature override.</summary>
    public double? Temperature { get; init; }

    /// <summary>Gets an optional cap on completion tokens.</summary>
    public int? MaxCompletionTokens { get; init; }
}
