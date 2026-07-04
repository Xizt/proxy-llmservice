namespace LlmShadow.Common.Options;

/// <summary>Configuration options for the SecondaryProcessor worker service.</summary>
public sealed class ProcessorOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Processor";

    /// <summary>Gets the maximum number of shadow messages processed concurrently.</summary>
    public int MaxDegreeOfParallelism { get; init; } = 4;
}
