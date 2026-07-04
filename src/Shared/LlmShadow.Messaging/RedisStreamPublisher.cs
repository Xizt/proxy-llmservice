using LlmShadow.Common.Options;
using LlmShadow.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LlmShadow.Messaging;

/// <summary>Redis Streams implementation of <see cref="IShadowQueuePublisher"/>.</summary>
public sealed class RedisStreamPublisher : IShadowQueuePublisher
{
    private readonly IDatabase _db;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisStreamPublisher> _logger;

    /// <summary>Initialises the publisher with a Redis connection and options.</summary>
    public RedisStreamPublisher(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisStreamPublisher> logger)
    {
        _db = redis.GetDatabase();
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync(ShadowQueueMessage message, CancellationToken cancellationToken = default)
    {
        var entries = new NameValueEntry[]
        {
            new("requestId",      message.RequestId.ToString()),
            new("payloadJson",    message.RequestPayloadJson),
            new("candidateModel", message.CandidateModel),
            new("retryCount",     message.RetryCount.ToString())
        };

        var msgId = await _db.StreamAddAsync(_options.StreamName, entries);

        _logger.LogDebug(
            "Published shadow message {RedisId} for request {RequestId} to stream {Stream}",
            msgId, message.RequestId, _options.StreamName);
    }
}
