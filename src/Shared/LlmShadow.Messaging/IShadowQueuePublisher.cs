using LlmShadow.Models.Common;

namespace LlmShadow.Messaging;

/// <summary>Publishes shadow execution messages to the Redis Stream queue.</summary>
public interface IShadowQueuePublisher
{
    /// <summary>
    /// Appends a <see cref="ShadowQueueMessage"/> to the Redis Stream.
    /// This method is safe to call fire-and-forget from the hot path; callers should catch
    /// and log exceptions rather than letting them propagate.
    /// </summary>
    /// <param name="message">The shadow message to enqueue.</param>
    /// <param name="cancellationToken">Token used to abort the publish.</param>
    Task PublishAsync(ShadowQueueMessage message, CancellationToken cancellationToken = default);
}
