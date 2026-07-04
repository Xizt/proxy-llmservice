namespace LlmShadow.Evaluation.Models;

/// <summary>The outcome of a deterministic heuristic evaluation comparing primary and candidate LLM responses.</summary>
public sealed record EvaluationResult
{
    /// <summary>Gets a value indicating whether the evaluation itself succeeded (i.e. both responses were valid JSON with an <c>action</c> key).</summary>
    public bool IsSuccess => FailureReason is null;

    /// <summary>Gets a value indicating whether the primary and candidate <c>action</c> values matched exactly.</summary>
    public bool IsMatch { get; init; }

    /// <summary>Gets the match percentage: <c>100.0</c> when matched, <c>0.0</c> when not matched or evaluation failed.</summary>
    public double MatchPercentage { get; init; }

    /// <summary>Gets the <c>action</c> value extracted from the primary response, or <c>null</c> when extraction failed.</summary>
    public string? PrimaryAction { get; init; }

    /// <summary>Gets the <c>action</c> value extracted from the candidate response, or <c>null</c> when extraction failed.</summary>
    public string? SecondaryAction { get; init; }

    /// <summary>Gets the reason the evaluation could not be completed. <c>null</c> when <see cref="IsSuccess"/> is <c>true</c>.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Creates a failed evaluation result with a descriptive <paramref name="reason"/>.</summary>
    public static EvaluationResult Failure(string reason) =>
        new() { FailureReason = reason, IsMatch = false, MatchPercentage = 0.0 };
}
