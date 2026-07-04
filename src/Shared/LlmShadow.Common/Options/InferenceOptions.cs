namespace LlmShadow.Common.Options;

/// <summary>Configuration options for DigitalOcean Serverless Inference API calls.</summary>
public sealed class InferenceOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Inference";

    /// <summary>Gets the DO Inference base URL (OpenAI-compatible).</summary>
    public string BaseUrl { get; init; } = "https://inference.do-ai.run/v1";

    /// <summary>Gets the model access key for Bearer authentication.</summary>
    public string ModelAccessKey { get; init; } = string.Empty;

    /// <summary>Gets the model identifier for the primary (hot-path) LLM.</summary>
    public string PrimaryModel { get; init; } = string.Empty;

    /// <summary>Gets the model identifier for the candidate (shadow) LLM.</summary>
    public string CandidateModel { get; init; } = string.Empty;

    /// <summary>Gets the per-request timeout in seconds for the primary streaming call.</summary>
    public int PrimaryTimeoutSeconds { get; init; } = 60;

    /// <summary>Gets the per-request timeout in seconds for the candidate non-streaming call.</summary>
    public int CandidateTimeoutSeconds { get; init; } = 120;

    /// <summary>Gets the number of automatic retries for transient failures on non-streaming calls.</summary>
    public int RetryCount { get; init; } = 2;
}
