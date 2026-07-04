using LlmShadow.Models.Common;

namespace LlmShadow.Messaging;

/// <summary>Consumes shadow execution messages from the Redis Stream consumer group.</summary>
public interface IShadowQueueConsumer
{
    /// <summary>
    /// Initialises the consumer group on the Redis Stream, creating both the stream and the group
    /// if they do not already exist. This must be called once before <see cref="ReadBatchAsync"/>.
    /// </summary>
    Task EnsureConsumerGroupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads up to <paramref name="count"/> new messages from the stream using XREADGROUP.
    /// Blocks for up to the configured <c>BlockTimeoutMs</c> when the stream is empty.
    /// </summary>
    /// <param name="count">Maximum number of messages to return.</param>
    /// <param name="cancellationToken">Token used to abort the read.</param>
    /// <returns>A list of (redisMessageId, message) tuples ready for processing.</returns>
    Task<IReadOnlyList<(string MessageId, ShadowQueueMessage Message)>> ReadBatchAsync(
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims pending messages that have been idle for longer than the configured threshold.
    /// Called periodically to recover messages that were read but never acknowledged.
    /// </summary>
    /// <param name="count">Maximum number of stale messages to claim.</param>
    /// <param name="cancellationToken">Token used to abort the operation.</param>
    /// <returns>A list of re-claimed (redisMessageId, message) tuples.</returns>
    Task<IReadOnlyList<(string MessageId, ShadowQueueMessage Message)>> ClaimStalePendingAsync(
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>Acknowledges successful processing of the message with <paramref name="messageId"/>.</summary>
    Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>Moves a message to the dead-letter stream after exhausting all retries.</summary>
    Task DeadLetterAsync(string messageId, ShadowQueueMessage message, CancellationToken cancellationToken = default);
}
