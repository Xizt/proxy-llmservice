namespace LlmShadow.Common.Options;

/// <summary>Configuration options for the DO Managed Redis/Valkey shadow queue.</summary>
public sealed class RedisOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Redis";

    /// <summary>Gets the Redis connection string (host:port,password=...,ssl=True).</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Gets the name of the primary shadow queue Redis Stream.</summary>
    public string StreamName { get; init; } = "shadow:queue";

    /// <summary>Gets the name of the dead-letter Redis Stream for unprocessable messages.</summary>
    public string DeadLetterStreamName { get; init; } = "shadow:dead";

    /// <summary>Gets the consumer group name used by all SecondaryProcessor instances.</summary>
    public string ConsumerGroupName { get; init; } = "shadow-group";

    /// <summary>Gets the unique consumer name for this processor instance.</summary>
    public string ConsumerName { get; init; } = "processor-1";

    /// <summary>Gets the maximum number of messages to read per batch.</summary>
    public int BatchSize { get; init; } = 10;

    /// <summary>Gets the milliseconds to block on XREADGROUP when the stream is empty.</summary>
    public int BlockTimeoutMs { get; init; } = 5000;

    /// <summary>Gets the idle time in milliseconds after which a pending message is re-claimed.</summary>
    public int PendingClaimThresholdMs { get; init; } = 300_000;

    /// <summary>Gets the maximum number of delivery attempts before a message is dead-lettered.</summary>
    public int MaxRetryCount { get; init; } = 3;
}
