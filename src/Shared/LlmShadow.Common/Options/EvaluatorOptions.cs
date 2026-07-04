namespace LlmShadow.Common.Options;

/// <summary>Configuration options for the background heuristic evaluator worker.</summary>
public sealed class EvaluatorOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Evaluator";

    /// <summary>Gets the polling interval in seconds between evaluation batches.</summary>
    public int IntervalSeconds { get; init; } = 60;

    /// <summary>Gets the maximum number of records to evaluate per cycle.</summary>
    public int BatchSize { get; init; } = 100;
}
