using LlmShadow.Common.Options;
using LlmShadow.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LlmShadow.Messaging;

/// <summary>Redis Streams implementation of <see cref="IShadowQueueConsumer"/> with consumer-group semantics.</summary>
public sealed class RedisStreamConsumer : IShadowQueueConsumer
{
    private readonly IDatabase _db;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisStreamConsumer> _logger;

    /// <summary>Initialises the consumer with a Redis connection and options.</summary>
    public RedisStreamConsumer(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisStreamConsumer> logger)
    {
        _db = redis.GetDatabase();
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureConsumerGroupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(
                _options.StreamName,
                _options.ConsumerGroupName,
                StreamPosition.NewMessages,
                createStream: true);

            _logger.LogInformation(
                "Redis consumer group '{Group}' ensured on stream '{Stream}'",
                _options.ConsumerGroupName, _options.StreamName);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal))
        {
            _logger.LogDebug("Consumer group '{Group}' already exists on stream '{Stream}'",
                _options.ConsumerGroupName, _options.StreamName);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string MessageId, ShadowQueueMessage Message)>> ReadBatchAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var entries = await _db.StreamReadGroupAsync(
            _options.StreamName,
            _options.ConsumerGroupName,
            _options.ConsumerName,
            StreamPosition.UndeliveredMessages,
            count,
            noAck: false);

        if (entries is null || entries.Length == 0)
            return Array.Empty<(string, ShadowQueueMessage)>();

        return entries
            .Select(e => (e.Id.ToString(), DeserializeEntry(e)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string MessageId, ShadowQueueMessage Message)>> ClaimStalePendingAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var result = await _db.StreamAutoClaimAsync(
            _options.StreamName,
            _options.ConsumerGroupName,
            _options.ConsumerName,
            _options.PendingClaimThresholdMs,
            "0-0",
            count);

        if (result.ClaimedEntries is null || result.ClaimedEntries.Length == 0)
            return Array.Empty<(string, ShadowQueueMessage)>();

        return result.ClaimedEntries
            .Select(e => (e.Id.ToString(), DeserializeEntry(e)))
            .ToList();
    }

    /// <inheritdoc />
    public Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default) =>
        _db.StreamAcknowledgeAsync(_options.StreamName, _options.ConsumerGroupName, messageId);

    /// <inheritdoc />
    public async Task DeadLetterAsync(string messageId, ShadowQueueMessage message, CancellationToken cancellationToken = default)
    {
        var entries = new NameValueEntry[]
        {
            new("originalId",     messageId),
            new("requestId",      message.RequestId.ToString()),
            new("payloadJson",    message.RequestPayloadJson),
            new("candidateModel", message.CandidateModel),
            new("retryCount",     message.RetryCount.ToString()),
            new("deadLetteredAt", DateTime.UtcNow.ToString("O"))
        };

        await _db.StreamAddAsync(_options.DeadLetterStreamName, entries);
        await AcknowledgeAsync(messageId, cancellationToken);

        _logger.LogWarning(
            "Message {MessageId} for request {RequestId} moved to dead-letter stream after {Retries} retries",
            messageId, message.RequestId, message.RetryCount);
    }

    private static ShadowQueueMessage DeserializeEntry(StreamEntry entry)
    {
        var dict = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());

        return new ShadowQueueMessage
        {
            RequestId      = Guid.Parse(dict.GetValueOrDefault("requestId", Guid.Empty.ToString())!),
            RequestPayloadJson = dict.GetValueOrDefault("payloadJson", string.Empty)!,
            CandidateModel = dict.GetValueOrDefault("candidateModel", string.Empty)!,
            RetryCount     = int.TryParse(dict.GetValueOrDefault("retryCount"), out var r) ? r : 0
        };
    }
}
